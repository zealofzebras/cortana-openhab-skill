using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Sequence;
using OpenHAB.NetRestApi.RestApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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

            Add(new AttachmentPrompt(ServerPrompt, LoginValidatorAsync));
            Add(new TextPrompt(TextPrompt));



            // Define the main dialog and add it to the set.
            Add(new WaterfallDialog(MainDialog, new WaterfallStep[]
                {
             EnterLoginInformationStep,
            async (stepContext, cancellationToken) =>
            {
            var json = (Newtonsoft.Json.Linq.JObject)((Microsoft.Bot.Builder.TurnContext)stepContext.Context).Activity.Value;

                var profile = new UserProfile()
                {
                    Server = json["server"].ToString(),
                    Username = json["username"].ToString(),
                    Password = json["password"].ToString()
                };

                stepContext.Values[UserInfo] = profile;
                return await stepContext.EndDialogAsync(profile, cancellationToken);
            }
                }));
        }

        private async Task<DialogTurnResult> EnterLoginInformationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activity = stepContext.Context.Activity;

            var replyToConversation = activity.CreateReply("Login to openHAB");
            replyToConversation.Attachments = new List<Attachment>();

            var card = new AdaptiveCard();
            card.Speak = "<s>Please enter the login information for your openHAB server?</s>";
            card.Body.Add(new TextBlock() { Text = "Server" });
            card.Body.Add(new TextInput() { Placeholder = "Server", Value = "https://myopenhab.org:443", Id = "server", Style = TextInputStyle.Url });
            card.Body.Add(new TextInput() { Placeholder = "Username", Id = "username" });
            card.Body.Add(new TextInput() { Placeholder = "Password", Id = "password" });
            card.Actions.Add(new SubmitAction() { Title = "Login" });

            // Create the attachment.
            Attachment attachment = new Attachment() { ContentType = AdaptiveCard.ContentType, Content = card };

            replyToConversation.Attachments.Add(attachment);


            return await stepContext.PromptAsync(ServerPrompt, new PromptOptions
            {
                Prompt = replyToConversation
            }, cancellationToken);
        }

        private async Task<bool> LoginValidatorAsync(PromptValidatorContext<IList<Attachment>> promptContext, CancellationToken cancellationToken)
        {
            var json = (Newtonsoft.Json.Linq.JObject)((Microsoft.Bot.Builder.TurnContext)promptContext.Context).Activity.Value;


            if (json == null || !json.ContainsKey("server"))
            {
                await promptContext.Context.SendActivityAsync(
                    "Something went wrong and I did not receive any configuration",
                    cancellationToken: cancellationToken);
                return false;
            }

            if (string.IsNullOrWhiteSpace(json["server"].ToString()))
            {
                await promptContext.Context.SendActivityAsync(
                    "The server field is empty. Please tell me the openHAB server name. Like this: https://myopenhab.org:443",
                    cancellationToken: cancellationToken);
                return false;
            }

            if (!Uri.TryCreate(json["server"].ToString(), UriKind.Absolute, out var outUri)
               && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps))
            {
                await promptContext.Context.SendActivityAsync(
                    "I could not enterpret the server field as a url. Please tell me the openHAB server name. Like this: https://myopenhab.org:443",
                    cancellationToken: cancellationToken);
                return false;
            }

            if (outUri.ToString().Contains("/rest"))
            {
                await promptContext.Context.SendActivityAsync(
                    "Please leave out the \"/rest\" part of the url, I just need to know the root of your openhab installation, like this: https://myopenhab.org:443",
                    cancellationToken: cancellationToken);
                return false;
            }


            // Check reachability of the url
            try
            {
                var request = (HttpWebRequest)HttpWebRequest.Create(outUri);
                request.Timeout = 3000;
                request.AllowAutoRedirect = false; // find out if this site is up and don't follow a redirector
                request.Method = "HEAD";

                using (var response = request.GetResponse())
                {
                    // connected now check if we can login
                    try
                    {
                        var client = OpenHAB.NetRestApi.RestApi.OpenHab.CreateRestClient(outUri,
                            json["username"].ToString(), json["password"].ToString(), false);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        await promptContext.Context.SendActivityAsync(
                            "I was not able to connect to the server with the provided credentials, are you sure you gave the correct information?",
                            cancellationToken: cancellationToken);
                        return false;

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                await promptContext.Context.SendActivityAsync(
                    "I was not able to find the server online, are you sure you gave the correct information?",
                    cancellationToken: cancellationToken);
                return false;
            }

        }

    }
}
