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
using Newtonsoft.Json.Linq;
using OpenHAB.NetRestApi.RestApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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


                    var valueObj = JsonConvert.DeserializeObject<JObject>(turnContext.Activity.ChannelData.ToString());
                    var currentAudioObject = JsonConvert.DeserializeObject<JObject>(valueObj["currentAudioInfo"]?.ToString() ?? "");

                    if (currentAudioObject == null)
                    {
                        await turnContext.SendActivityAsync("Let's get started. For now I can only use basic authentication, which means I will ask for your username and password here.", "Let's get started", InputHints.IgnoringInput);

                        await dc.BeginDialogAsync(LoginDialog.MainDialog);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync("I need you to login on another device with a screen", "I cannot configure this service on a speaker only device, sorry.", InputHints.IgnoringInput);

                    }
                }
                else if (turnContext.Activity.Text.Trim().Replace(" ", "") == "logout")
                {
                    await turnContext.SendActivityAsync("Logging out", "Sorry to see you go", InputHints.IgnoringInput);
                    await _accessors.UserProfileAccessor.DeleteAsync(turnContext, cancellationToken);

                }
                else if (turnContext.Activity.Text == "show device data")
                {
                    await turnContext.SendActivityAsync("This is the devicedata:" + turnContext.Activity.ChannelData.ToString(), null, InputHints.IgnoringInput);
                }
                else if (turnContext.Activity.Text == "test connection")
                {
                    try
                    {
                        var client = OpenHAB.NetRestApi.RestApi.OpenHab.CreateRestClient(new Uri(profile.Server),
                            profile.Username, profile.Password, false);
                        await turnContext.SendActivityAsync("All is well", "I am connected to the openhab server", InputHints.IgnoringInput);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        await turnContext.SendActivityAsync("I experienced an error:" + ex.ToString(), null, InputHints.IgnoringInput);

                    }
                }
                else if (turnContext.Activity.Text == "enumerate items")
                {
                    try
                    {
                        await turnContext.SendActivityAsync("Please wait", "I am getting the list of items on the openhab instance", InputHints.IgnoringInput);

                        var client = OpenHAB.NetRestApi.RestApi.OpenHab.CreateRestClient(new Uri(new Uri(profile.Server), "rest/"),
                            profile.Username, profile.Password, false);


                        var items = await client.ItemService.GetItemsAsync(cancellationToken);

                        await turnContext.SendActivityAsync($"There are {items.Count} items", $"I found {items.Count} items on the openhab instance", InputHints.IgnoringInput);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        await turnContext.SendActivityAsync("I experienced an error:" + ex.ToString(), null, InputHints.IgnoringInput);

                    }
                }
                else
                {
                    var client = OpenHAB.NetRestApi.RestApi.OpenHab.CreateRestClient(new Uri(new Uri(profile.Server), "rest/"),
                        profile.Username, profile.Password, false);

                    try
                    {
                        const string resource = "/habot/chat";

                        var response = await client.ExecuteRequestAsync<openhab.HaBotResponse>(RestSharp.Method.POST, resource, turnContext.Activity.Text,
                            new OpenHAB.NetRestApi.Models.RequestHeaderCollection() { OpenHAB.NetRestApi.Models.RequestHeader.ContentPlainText },
                            token: cancellationToken);

                        var fullText = response.Answer;

                        if (!string.IsNullOrEmpty(response.Hint))
                            fullText += "\n\n" + response.Hint;



                        var reply = turnContext.Activity.CreateReply(fullText);
                        reply.Speak = response.Answer;
                        reply.InputHint = InputHints.AcceptingInput;

                        if (response.Card != null)
                        {
                            reply.Attachments = new List<Attachment>();

                            if (string.IsNullOrWhiteSpace(response.Card.Title))
                                reply.Speak = "The " + response.Card.Title + "is";

                            var card = new AdaptiveCards.AdaptiveCard();
                            card.Title = response.Card.Title;
                            if (response.Card.Slots?.List != null)
                                foreach (var slot in response.Card.Slots.List)
                                {
                                    card.Body.Add(await SlotToElement(slot, client));
                                }

                            if (response.Card.Slots?.Right != null)
                                foreach (var slot in response.Card.Slots.Right)
                                {
                                    card.Body.Add(await SlotToElement(slot, client));
                                }

                            // Create the attachment.
                            Attachment attachment = new Attachment() { ContentType = AdaptiveCards.AdaptiveCard.ContentType, Content = card };

                            reply.Attachments.Add(attachment);

                        }


                        await turnContext.SendActivityAsync(reply);

                    }
                    catch (Exception ex)
                    {
                        await turnContext.SendActivityAsync("I am not able to connect to the openHAB", "I am not able to connect to the openHAB", InputHints.IgnoringInput);
                    }

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


        public async Task<AdaptiveCards.CardElement> SlotToElement(openhab.Slot slot, OpenHabRestClient client)
        {
            switch (slot.Component)
            {
                case "HbSingleItemValue":

                    var singleItem = new AdaptiveCards.TextBlock();
                    if (slot.Config.Label != null)
                        singleItem.Text = slot.Config.Label + " = ";

                    var singleItemState = await client.ItemService.GetItemStateAsync(slot.Config.Item);

                    singleItem.Text += singleItemState.Content;
                    return singleItem;

                case "HbList":

                    var list = new AdaptiveCards.FactSet();
                    foreach (var slotItem in slot.Slots.Items)
                    {
                        var fact = new AdaptiveCards.Fact();
                        var itemState = await client.ItemService.GetItemStateAsync(slotItem.Config.Item);

                        fact.Speak = $"{slotItem.Config.Label} is {itemState.Content}";

                        fact.Title = slotItem.Config.Label;
                        fact.Value = itemState.Content;
                        list.Facts.Add(fact);
                    }
                    return list;

                default:
                    return new AdaptiveCards.TextBlock() { Text = "Component: " + slot.Component };
            }
        }

    }
}
