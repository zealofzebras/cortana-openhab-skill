﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace openHAbot
{
    /// <summary>
    /// The Startup class configures services and the request pipeline.
    /// </summary>
    public class Startup
    {
        private ILoggerFactory _loggerFactory;
        private readonly bool _isProduction;

        public Startup(IHostingEnvironment env)
        {
            // Enable fiddler for debug
#if DEBUG
            OpenHAB.NetRestApi.RestApi.OpenHab.Proxy = new System.Net.WebProxy("127.0.0.1", 8888);
#endif


            _isProduction = env.IsProduction();
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> specifies the contract for a collection of service descriptors.</param>
        /// <seealso cref="IStatePropertyAccessor{T}"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/dependency-injection"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/azure/bot-service/bot-service-manage-channels?view=azure-bot-service-4.0"/>
        public void ConfigureServices(IServiceCollection services)
        {

            // The Memory Storage used here is for local bot debugging only. When the bot
            // is restarted, everything stored in memory will be gone.
            // IStorage dataStore = new MemoryStorage();

            // Default container name.
            const string DefaultBotContainer = "openhabot";
            var configContainer = Configuration.GetValue<string>("storageContainer");
            var configConnectionString = Configuration.GetValue<string>("storageConnectionString");
            var storageContainer = string.IsNullOrWhiteSpace(configContainer) ? DefaultBotContainer : configContainer;
            IStorage dataStore = new Microsoft.Bot.Builder.Azure.AzureBlobStorage(configConnectionString, storageContainer);

            // Create Conversation State object.
            // The Conversation State object is where we persist anything at the conversation-scope.
            var conversationState = new ConversationState(dataStore);
            var userState = new UserState(dataStore);

            services.AddBot<openHAbotBot>(options =>
           {
               var secretKey = Configuration.GetSection("botFileSecret")?.Value;
               var botFilePath = Configuration.GetSection("botFilePath")?.Value;

               // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
               var botConfig = BotConfiguration.Load(botFilePath ?? @".\openHAbot.bot", secretKey);
               services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot config file could not be loaded. ({botConfig})"));

               // Retrieve current endpoint.
               var environment = _isProduction ? "production" : "development";
               var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
               if (!(service is EndpointService endpointService))
               {
                   throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
               }
               // Creates a logger for the application to use.
               ILogger logger = _loggerFactory.CreateLogger<openHAbotBot>();
               logger.LogError($"appId : {endpointService.AppId}");
               //logger.LogError($"appId : {endpointService.AppId}");

               string microsoftAppId = Configuration[MicrosoftAppCredentials.MicrosoftAppIdKey];
               string microsoftAppPassword = Configuration[MicrosoftAppCredentials.MicrosoftAppPasswordKey];


               options.CredentialProvider = new SimpleCredentialProvider(microsoftAppId, microsoftAppPassword);
             

               // Catches any errors that occur during a conversation turn and logs them.
               options.OnTurnError = async (context, exception) =>
              {
                  logger.LogError($"Exception caught : {exception}");
                  await context.SendActivityAsync("Sorry, it looks like something went wrong.");
              };

           });

            // Create and register state accessors.
            // Accessors created here are passed into the IBot-derived class on every turn.
            services.AddSingleton(sp =>
           {
               var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
               if (options == null)
               {
                   throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the state accessors");
               }

               if (conversationState == null)
               {
                   throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
               }

               // Create the custom state accessor.
               // State accessors enable other components to read and write individual properties of state.
               var accessors = new openHAbotAccessors(conversationState, userState)
               {
                   CounterState = conversationState.CreateProperty<CounterState>(openHAbotAccessors.CounterStateName),
                   LoginFlowAccessor = conversationState.CreateProperty<LoginFlow>(openHAbotAccessors.LoginFlowName),
                   UserProfileAccessor = userState.CreateProperty<UserProfile>(openHAbotAccessors.UserProfileName),
                   DialogStateAccessor = conversationState.CreateProperty<DialogState>(openHAbotAccessors.DialogStateName),

               };

               return accessors;
           });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
