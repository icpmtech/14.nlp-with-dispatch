// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Microsoft.BotBuilderSamples
{
    public class BotServices : IBotServices
    {
        IConfiguration configuration;
        public BotServices(IConfiguration _configuration)
        {
             configuration = _configuration;
            // Read the setting for cognitive services (LUIS, QnA) from the appsettings.json
            // If includeApiResults is set to true, the full response from the LUIS api (LuisResult)
            // will be made available in the properties collection of the RecognizerResult

            var luisApplication = new LuisApplication(
                configuration["LuisAppId"],
                configuration["LuisAPIKey"],
               $"https://{configuration["LuisAPIHostName"]}.api.cognitive.microsoft.com");

            var luisApplicationDetails = new LuisApplication(
              configuration["LuisAppIdDetails"],
              configuration["LuisAPIKeyDetails"],
             $"https://{configuration["LuisAPIHostName"]}.api.cognitive.microsoft.com");

            // Set the recognizer options depending on which endpoint version you want to use.

            var recognizerOptions = new LuisRecognizerOptionsV3(luisApplication)
            {
                IncludeAPIResults = true,
                PredictionOptions = new Bot.Builder.AI.LuisV3.LuisPredictionOptions()
                {
                    IncludeAllIntents = true,
                    IncludeAPIResults=true,
                    IncludeInstanceData = true,
                   
                }
            };
            var recognizerOptionsDetails = new LuisRecognizerOptionsV3(luisApplicationDetails)
            {
                IncludeAPIResults = true,
                PredictionOptions = new Bot.Builder.AI.LuisV3.LuisPredictionOptions()
                {
                    IncludeAllIntents = true,
                    IncludeAPIResults = true,
                    IncludeInstanceData = true,

                }
            };

            Dispatch = new LuisRecognizer(recognizerOptions);

            DispatchDetailsModels = new LuisRecognizer(recognizerOptions);

            QnAMakerService = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId = configuration["QnAKnowledgebaseId"],
                EndpointKey = configuration["QnAEndpointKey"],
                Host = GetHostname()
            });
        }
       
        private string GetHostname()
        {
            var hostname = configuration["QnAEndpointHostName"];
            if (!hostname.StartsWith("https://"))
            {
                hostname = string.Concat("https://", hostname);
            }

            if (!hostname.EndsWith("/qnamaker"))
            {
                hostname = string.Concat(hostname, "/qnamaker");
            }

            return hostname;
        }
        public LuisRecognizer Dispatch { get; private set; }
        public LuisRecognizer DispatchDetailsModels { get; private set; }
        public QnAMaker QnAMakerService { get; private set; }
    }
}
