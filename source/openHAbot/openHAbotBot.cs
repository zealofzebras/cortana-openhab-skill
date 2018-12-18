// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Choice;
using Microsoft.Recognizers.Text.Sequence;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace openHAbot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class openHAbotBot : IBot
    {
        private readonly openHAbotAccessors _accessors;
        private readonly ILogger _logger;
        /// <summary>The dialog set that has the dialog to use.</summary>
        private LoginDialog loginDialog { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public openHAbotBot(openHAbotAccessors accessors, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<openHAbotBot>();
            _logger.LogTrace("Turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
            // Create the greetings dialog.
            loginDialog = new LoginDialog(_accessors.DialogStateAccessor);
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {


                var profile = await _accessors.UserProfileAccessor.GetAsync(turnContext, () => new UserProfile());
                // Establish context for our dialog from the turn context.
                DialogContext dc = await loginDialog.CreateContextAsync(turnContext);



                // If there's a dialog running, continue it.
                if (dc.ActiveDialog != null)
                {
                    var dialogTurnResult = await dc.ContinueDialogAsync();
                    if (dialogTurnResult.Status == DialogTurnStatus.Complete
                        && dialogTurnResult.Result is UserProfile _profile
                        && _profile != null)
                    {
                        // If it completes successfully and returns a valid name, save the name and greet the user.
                        profile = _profile;
                        profile.Valid = true;
                        await turnContext.SendActivityAsync($"Loggin on to {profile.Server}.");

                        // Update the user data in the turn's state cache.
                        await _accessors.UserProfileAccessor.SetAsync(turnContext, profile, cancellationToken);
                    }
                }
                else if (profile.Valid == false)
                {

                    await turnContext.SendActivityAsync("Let's get started. For now I can only use basic authentication, which means I will ask for your username and password here.");
                    // Else, if we don't have the user's name yet, ask for it.
                    await dc.BeginDialogAsync(LoginDialog.MainDialog);
                }
                else
                {

                    var uri = new Uri(profile.Server).GetLeftPart(UriPartial.Authority) + "/rest/habot/chat";
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", profile.Username, profile.Password))));

                    var content = new StringContent(turnContext.Activity.Text);
                    var result = await client.PostAsync(uri, content);
                    var responseJson = await result.Content.ReadAsStringAsync();
                    var response = JsonConvert.DeserializeObject<openhab.Response>(responseJson);
                    await turnContext.SendActivityAsync(response.answer);
                    if (!string.IsNullOrEmpty(response.hint))
                        await turnContext.SendActivityAsync(response.hint);



                    /*
                     * {"language":"en","query":"Turn on outside lights","answer":"I found nothing to switch on...","hint":"I was expecting to find relevant items with \"lights\" but none matched.","intent":{"name":"activate-object","entities":{"object":"lights"}}}
                     */
                    // Else, echo the user's message text.
                    //await turnContext.SendActivityAsync($"{profile.Server} did not expect, '{turnContext.Activity.Text}'.");
                }



                // Persist any changes to storage.
                await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _accessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);



            }
            else
            {
                // await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected");
            }
        }




    }
}
