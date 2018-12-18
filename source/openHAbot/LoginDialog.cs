using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Sequence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace openHAbot
{
    public class LoginDialog : DialogSet

    {
        /// <summary>The ID of the main dialog.</summary>
        public const string MainDialog = "main";

        private const string UsePublicServerPrompt = "publicServer";
        private const string ServerPrompt = "serverUrl";
        private const string TextPrompt = "textPrompt";
        private const string UserInfo = "userInfo";

        /// <summary>Creates a new instance of this dialog set.</summary>
        /// <param name="dialogState">The dialog state property accessor to use for dialog state.</param>
        public LoginDialog(IStatePropertyAccessor<DialogState> dialogState)
            : base(dialogState)
        {




            Add(new ConfirmPrompt(UsePublicServerPrompt));
            // Add the text prompt to the dialog set.

            Add(new TextPrompt(ServerPrompt, ServerUrlValidatorAsync));
            Add(new TextPrompt(TextPrompt));


            // Define the main dialog and add it to the set.
            Add(new WaterfallDialog(MainDialog, new WaterfallStep[]
            {
            async (stepContext, cancellationToken) =>
            {
                stepContext.Values[UserInfo] = new UserProfile();
                return await stepContext.PromptAsync(
                    UsePublicServerPrompt,
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Are you using the public myopenhab.org server?"),                        
                        RetryPrompt = MessageFactory.Text("Sorry, please let me know if you are using the public server"),
                    },
                    cancellationToken);
            },
            async (stepContext, cancellationToken) =>
            {
                if (!(bool)stepContext.Result)
                {

                    // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
                    return await stepContext.PromptAsync(ServerPrompt, new PromptOptions {
                        Prompt = MessageFactory.Text("Which server url do you want to connect to?.")
                    }, cancellationToken);
                }
                else
                {
                    ((UserProfile)stepContext.Values[UserInfo]).Server = "https://myopenhab.org:443/"; 

                    // User said "yes" so we will skip the next step. Give -1 as the age.
                    return await stepContext.NextAsync("https://myopenhab.org:443/", cancellationToken);
                }
            },
            async (stepContext, cancellationToken) =>
            {
                ((UserProfile)stepContext.Values[UserInfo]).Server = (string)stepContext.Result;

                return await stepContext.PromptAsync(
                    TextPrompt,
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("What is your username?"),
                        RetryPrompt = MessageFactory.Text("Sorry, what is your username?"),
                    },
                    cancellationToken);
            },
            async (stepContext, cancellationToken) =>
            {
                ((UserProfile)stepContext.Values[UserInfo]).Username = (string)stepContext.Result;
                return await stepContext.PromptAsync(
                    TextPrompt,
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("What is your password?"),
                        RetryPrompt = MessageFactory.Text("Sorry, what is your password?"),
                    },
                    cancellationToken);
            },

            async (stepContext, cancellationToken) =>
            {
                var profile = ((UserProfile)stepContext.Values[UserInfo]);
                profile.Password = (string)stepContext.Result;
                // Assume that they entered their name, and return the value.
                return await stepContext.EndDialogAsync(profile, cancellationToken);
            },
            }));
        }

        private async Task<bool> ServerUrlValidatorAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Check whether the input could be recognized as an integer.
            if (!promptContext.Recognized.Succeeded)
            {
                await promptContext.Context.SendActivityAsync(
                    "I'm sorry, I could not interpret that as a url. Please tell me the openHAB server url.",
                    cancellationToken: cancellationToken);
                return false;
            }


            var results = SequenceRecognizer.RecognizeURL(promptContext.Recognized.Value, Culture.English);
            foreach (var result in results)
            {
                if (result.Resolution.TryGetValue("value", out object value))
                {

                    var server = value.ToString();
                    // TODO, reachable?
                    return true;
                }
            }
            await promptContext.Context.SendActivityAsync(
                "I could not interpret that as a url. Please tell me the openHAB server name. Like this: https://myopenhab.org:443",
                cancellationToken: cancellationToken);

            return false;
        }

    }
}

/*
    
     */
