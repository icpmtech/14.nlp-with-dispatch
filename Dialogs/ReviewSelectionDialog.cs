// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QnABot.DAL;
using QnABot.Models;


namespace Tutorial.Bot
{
    /// <summary>
    /// Menu child the review questions make by the user
    /// </summary>
    public class ReviewSelectionDialog : ComponentDialog
    {
        TokensContext tokensContext;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string DoneOption = "Terminar";
        private const string NoSelected = "value-noSelected";
        private const string PromptFirstQuestionBot = "Coloca me tua questão?";
        private const string PromptSecondStepQuestionBot = "Vamos tentar outra vez, por favor reformule a questão.";
        private const string PromptThirdStepQuestionBot = "Vamos tentar outra vez, por favor reformule a questão.";
        private const string UserInfo = "value-userInfo";
        private  HttpClient client;
        // Define the company choices for the company selection prompt.
        private readonly string[] _satisfatoryOptions = new string[]
        {
           "Não"
        };
        /// <summary>
        /// Method to make a review with the choice of the end user
        /// </summary>
        /// <param name="configuration">The configuration get from appsettings</param>
        /// <param name="httpClientFactory">The Http client to make a request to QaNMaker</param>
        public ReviewSelectionDialog(IConfiguration configuration, IHttpClientFactory httpClientFactory, TokensContext _tokensContext)
            : base(nameof(ReviewSelectionDialog))
        {
            client = new HttpClient { BaseAddress = new Uri(configuration["HostHttpAPI"]) };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _configuration = configuration;
            tokensContext = _tokensContext;
            _httpClientFactory = httpClientFactory;
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
                {
                    QuestionStepAsync,
                    SelectionStepAsync,
                    LoopStepAsync,
                }));

            InitialDialogId = nameof(WaterfallDialog);
        }
        /// <summary>
        /// This is the first step to the bot
        /// </summary>
        /// <param name="stepContext">WaterfallStepContext context</param>
        /// <param name="cancellationToken">Token</param>
        /// <returns>A message to the user with a TextPrompt</returns>
        private  async Task<DialogTurnResult> QuestionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            var list = stepContext.Options as List<string> ?? new List<string>();
            stepContext.Values[NoSelected] = list;
            string userInput = stepContext.Context.Activity.Text;
            string splitedInput = stepContext.Context.Activity.Text;
            var promptOptions = new PromptOptions { Prompt = MessageFactory.Text(PromptFirstQuestionBot) };
            if (stepContext.Context.Activity?.ChannelId=="msteams")
            {
                splitedInput = RemoveMentionInTeams(userInput, splitedInput);
            }


            if (list.Count is 0 && !string.IsNullOrEmpty(splitedInput) && !string.IsNullOrWhiteSpace(splitedInput))
            {
               
                return await stepContext.NextAsync(list, cancellationToken);
            }
           else if (list.Count is 1)
            {
                promptOptions.Prompt = MessageFactory.Text(PromptSecondStepQuestionBot);
                // Ask the user to enter their question.
                return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
            }
            else if (list.Count is 2)
            {
                promptOptions.Prompt = MessageFactory.Text(PromptThirdStepQuestionBot);
                // Ask the user to enter their question.
                return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
            }
            else
            {
                promptOptions.Prompt = MessageFactory.Text(PromptFirstQuestionBot);
                // Ask the user to enter their question.
                return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
            }

        }

        private static string RemoveMentionInTeams(string userInput, string splitedInput)
        {
            if (userInput.Contains("</at>"))
            {
                string[] splitInput = userInput.Split("</at>");
                splitedInput = splitInput.Length > 0 ? splitInput[1] : string.Empty;
            }

            return splitedInput;
        }

        /// <summary>
        /// This method step validates the choice from the end user
        /// </summary>
        /// <param name="stepContext">WaterfallStepContext from choice</param>
        /// <param name="cancellationToken">The cancellation token to the task</param>
        /// <returns></returns>
        private async Task<DialogTurnResult> SelectionStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            var list = stepContext.Options as List<string> ?? new List<string>();
            stepContext.Values[NoSelected] = list;
            await MakeRequestToGetAnswerAsync(stepContext, cancellationToken);
            // Create a prompt message.
            string message;
            if (list.Count is 0)
            {
                message = $"A sua questão ficou resolvida? Se sim, clique em `{DoneOption}`.";
            }
            else
            {
                message = $"A sua questão ficou resolvida? Se sim, clique em `{DoneOption}`.";

            }

            // Create the list of options to choose from.
            var options = _satisfatoryOptions.ToList();
            options.Add(DoneOption);
            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text(message),
                RetryPrompt = MessageFactory.Text("Hmm, não encontrei a resposta pretendida. Vamos tentar outra vez, por favor reformule a questão."),
                Choices = ChoiceFactory.ToChoices(options),
            };

            // Prompt the user for a choice.
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        /// <summary>
        /// To Get the Host Name
        /// </summary>
        /// <returns>The host name</returns>
        private string GetHostname()
        {
            var hostname = _configuration["QnAEndpointHostName"];
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

        /// <summary>
        /// Make a request based in a question maked by the user
        /// </summary>
        /// <param name="stepContext">The context</param>
        /// <param name="cancellationToken">the cancelation Token</param>
        /// <returns>The answer to the question asked</returns>
        private async Task MakeRequestToGetAnswerAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
           await ProcessResponse(stepContext, cancellationToken).ConfigureAwait(false);
            //await DummyDataProcess(stepContext).ConfigureAwait(false);
        }

        private async Task ProcessResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[UserInfo] = new UserProfile();
            string question = stepContext.Context.Activity.Text;
            var userProfile = (UserProfile)stepContext.Values[UserInfo];
            userProfile.Name = stepContext.Context.Activity.From.Name;
            userProfile.TokenId = stepContext.Context.Activity.From.Id;

            if (stepContext.Context.Activity?.ChannelId == "msteams")
            {
                question = RemoveMentionInTeams(question, question);
                userProfile.Questions.Add(question);
            }
            else
            {
                userProfile.Questions.Add(question);
            }

            var httpClient = _httpClientFactory.CreateClient();
            var qnaMaker = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId = _configuration["QnAKnowledgebaseId"],
                EndpointKey = _configuration["QnAAuthKey"],
                Host = GetHostname()
            },
          null,
          httpClient);

            // The actual call to the QnA Maker service.
            DateTime beginAskTimeQnA = DateTime.Now;
            var response = await qnaMaker.GetAnswersAsync(stepContext.Context);
            DateTime endAskTimeQnA = DateTime.Now;
            if (response != null && response.Length > 0)
            {
                var answer = response[0].Answer;
                var score = response[0].Score;
                var source = response[0].Source;
                Activity reply = MessageFactory.Text($"{answer}");
                await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                await SaveRequestToStatsAsync(question, answer, beginAskTimeQnA, endAskTimeQnA, score, source, userProfile);

            }
            else
            {
                Activity reply = MessageFactory.Text("Hmm, não encontrei a resposta pretendida. Vamos tentar outra vez, por favor reformule a questão.");
                await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                await SaveRequestToStatsAsync(question, "SEM_RESPOSTA_VALIDA", beginAskTimeQnA, endAskTimeQnA, 0, "N_SOURCE", userProfile).ConfigureAwait(false);

            }
        }

        private async Task TesteData(WaterfallStepContext stepContext)
        {
            await DummyDataProcess(stepContext).ConfigureAwait(false);
        }

        private async Task DummyDataProcess(WaterfallStepContext stepContext)
        {
            var lisQuestions = new List<string>();
            lisQuestions.AddRange(new[]{ "O que são os Coronavírus?",
"O que é o Novo Coronavírus?",
"COVID-19 é o mesmo que o SARS-CoV-2?",
"Qual é a origem do Novo Coronavírus?",
"Já houve algum surto com Coronavírus em anos anteriores?",
"Quais os sinais e sintomas?",
"Os sintomas de COVID-19 são diferentes nas crianças e nos adultos?",
"As pessoas que têm a doença ficam imunes?",
"A COVID-19 é o mesmo que gripe?",
"Qual a diferença entre epidemia e pandemia?",
"Como se transmite?",
"Qual é o período de incubação?",
"O que quer dizer transmissão comunitária?",
"Antes do aparecimento de sintomas, a pessoa pode transmitir a infeção?",
"O SARS-CoV-2 pode ser transmitido através das fezes?",
"A COVID-19 pode ser transmitida através de alimentos, incluindo os refrigerados e congelados?",
"O clima quente vai parar o surto de COVID-19?",
"Quanto tempo o vírus persiste numa superfície?",
"O dinheiro é um veículo de transmissão da COVID-19?",
"O que são medidas de higiene e etiqueta respiratória?",
"O que é que as pessoas em risco de doença grave por COVID-19 devem fazer?",
"Tenho de usar máscara para me proteger?",
"Como devo colocar e retirar uma máscara?",
"Quando é recomendado o uso de máscara, esta pode ser substituída por viseira?",
"Devo desinfetar tablets, smartphones e computadores?",
"Que cuidados devo ter na preparação e confeção de alimentos?",
"O que se recomenda a quem tem que andar de metro/autocarro?",
"Os utentes devem contactar os CSP diretamente ou apenas através do SNS24?",
"No caso de contactarem diretamente, onde podem obter os contactos? Há algum diretório?",
"Retoma das atividades. E agora?",
"Quando posso abrir o meu estabelecimento/ empresa/serviço e qual o horário recomendado?",
"É obrigatório elaborar um plano de contingência sobre os procedimentos a tomar perante a identificação de um caso suspeito de COVID-19? Este deve ser público?",
"Quando não for possível ter uma sala dedicada/própria para o “isolamento” num estabelecimento/empresa, como se deve proceder?",
"É obrigatória a redução da lotação máxima dos estabelecimentos de restauração e bebidas exemplificada no anexo da Orientação nº 023/2020?",
"Qual é a lotação máxima dos estabelecimentos de restauração e bebida? Deve estar afixada na entrada?",
"Os corredores de passagem devem ter 4 metros para os clientes circularem (2 metros para cada lada do cliente em deslocação)?",
"As barreiras físicas, de materiais como acrílico, vidro ou cortinas, podem servir para reduzir o distanciamento de 2 metros?",
"O uso de máscara pelos clientes é obrigatório na entrada e circulação pelo estabelecimento de restauração e bebidas?",
"Como fazer quando os clientes, devido ao teor do serviço prestado, não podem usar máscara (por exemplo, tratamentos de rosto, cortar a barba, comer, etc)?",
"Para servir alimentos ou bebida deve ser dada preferência aos materiais descartáveis ou pode continuar a usar-se loiça? É possível lavar a loiça sem máquina?",
"Quais os cuidados a ter em isolamento?",
"O que deve ter em conta os outros membros da casa?",
"Se estiver em isolamento, pode receber pessoas em casa?",
"O que não posso fazer em isolamento?",
"Porque culpam ou evitam pessoas e grupos devido à COVID-19 (Estigmatização)?",
"Como é que as pessoas podem ajudar a acabar com o estigma relacionado com a COVID-19?",
"As mulheres grávidas são mais suscetíveis à infeção ou têm maior risco de doenças graves, morbidade ou mortalidade com a COVID-19, em comparação com o público em geral?",
"As mulheres grávidas com COVID-19 têm risco aumentado de desfecho adverso na gravidez?",
"As profissionais de saúde grávidas correm maior risco de resultados adversos se cuidarem de pacientes com COVID-19?",
"As mulheres grávidas com COVID-19 podem transmitir o vírus ao feto ou ao recém-nascido (isto é, transmissão vertical)?",
"As crianças nascidas de mães infetadas com COVID-19 durante a gravidez correm maior risco de terem complicações?",
"Qual é o risco de que a existência da COVID-19 numa mulher grávida ou num recém-nascido possa ter efeitos a longo prazo na saúde e no desenvolvimento infantil que venham a requerer apoio clínico para além da infância?",
"A doença materna com COVID-19 durante o período de aleitamento materno está associada a um risco potencial para uma criança que é amamentada?",
"Qual é o tratamento?",
"Os antibióticos são efetivos a prevenir e tratar a COVID-19?",
"Não sei se funcionas...",
"Olá Assitente...",
"Existe uma vacina?",
"Onde posso fazer o teste?",});
            for (int i = 0; i < 100000; i++)
            {
                var random = new Random();
                int valueRandom = random.Next(0, 57);
                stepContext.Context.Activity.Text = lisQuestions[valueRandom];
                Thread.Sleep(100);
                stepContext.Values[UserInfo] = new UserProfile();
                string question = stepContext.Context.Activity.Text;
                var userProfile = (UserProfile)stepContext.Values[UserInfo];
                userProfile.Name = stepContext.Context.Activity.From.Name;
                userProfile.TokenId = stepContext.Context.Activity.From.Id;

                if (stepContext.Context.Activity?.ChannelId == "msteams")
                {
                    question = RemoveMentionInTeams(question, question);
                    userProfile.Questions.Add(question);
                }
                else
                {
                    userProfile.Questions.Add(question);
                }

                var httpClient = _httpClientFactory.CreateClient();
                var qnaMaker = new QnAMaker(new QnAMakerEndpoint
                {
                    KnowledgeBaseId = _configuration["QnAKnowledgebaseId"],
                    EndpointKey = _configuration["QnAAuthKey"],
                    Host = GetHostname()
                },
              null,
              httpClient);

                // The actual call to the QnA Maker service.

                DateTime beginAskTimeQnA = DateTime.Now;
                var response = await qnaMaker.GetAnswersAsync(stepContext.Context);
                DateTime endAskTimeQnA = DateTime.Now;
                if (response != null && response.Length > 0)
                {
                    var answer = response[0].Answer;
                    var score = response[0].Score;
                    var source = response[0].Source;
                    Activity reply = MessageFactory.Text($"{answer}");
                    //  await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                    await SaveRequestToStatsAsync(question, answer, beginAskTimeQnA, endAskTimeQnA, score, source, userProfile);

                }
                else
                {
                    Activity reply = MessageFactory.Text("Hmm, não encontrei a resposta pretendida. Vamos tentar outra vez, por favor reformule a questão.");
                    //  await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                    await SaveRequestToStatsAsync(question, "SEM_RESPOSTA_VALIDA", beginAskTimeQnA, endAskTimeQnA, 0, "N_SOURCE", userProfile).ConfigureAwait(false);

                }
            }
        }
        private async Task SaveRequestToStatsAsync(string question, string answer, DateTime beginAskTimeQnA, DateTime endAskTimeQnA, float score, string source, UserProfile userProfile)
        {
            try
            {
                QnABot.DAL.Token token = null;
                if (userProfile!=null)
                {
                    
                    token= await GetTokenByIdAsync(userProfile.Name).ConfigureAwait(false);
                   
                }
                if (token!=null)
                {
                    var stats = new Stats { CreateDate = DateTime.Now, Question = $"{question}", Answer = $"{answer}",Token= token, BeginAskTimeQnA= beginAskTimeQnA, EndAskTimeQnA= endAskTimeQnA,ScoreConfidence=score, SourceQnA=source };
                   await UpdateStatsAsync(stats).ConfigureAwait(false);
                }
                else
                {
                    var stats = new Stats { CreateDate = DateTime.Now, Question = $"{question}", Answer = $"{answer}", BeginAskTimeQnA = beginAskTimeQnA, EndAskTimeQnA = endAskTimeQnA, ScoreConfidence = score, SourceQnA = source };
                    await UpdateStatsAsync(stats).ConfigureAwait(false);
                    return;
                }


            }
            catch (Exception ex)
            {
                try
                {
                    await UpdateLogAsync(new Log { Error = "SaveRequestToStats", ErrorMessage = ex.Message, ExceptionValue = $"{ex.StackTrace}" }).ConfigureAwait(false);
                }
                catch (Exception submitChanges)
                {
                    throw submitChanges;
                }
                
            }
        }

        private async Task UpdateStatsAsync(Stats stats)
        {
            var response = await client.PostAsJsonAsync("api/Stats", stats);
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            else
            {
                return;
            }
        }
        private async  Task UpdateLogAsync(Log log)
        {
            
            HttpResponseMessage response = await client.PostAsJsonAsync("api/Logs", log);
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            else
            {
                return ;
            }
        }
        private async Task<QnABot.DAL.Token> GetTokenByIdAsync(string tokenId)
        {
            if (tokenId==string.Empty)
            {
                return null;
            }
            var response = client.GetAsync($"api/Tokens/{tokenId}").Result;
            if (response.IsSuccessStatusCode)
            {
              var  token= await response.Content.ReadAsAsync<QnABot.DAL.Token>();
              return token as QnABot.DAL.Token;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// The Loop Step to the limit of 3 No Questions give by the end user or a Done with the value true selected by the end user.
        /// </summary>
        /// <param name="stepContext">WaterfallStepContext </param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var list = stepContext.Values[NoSelected] as List<string>;
           
            
            var choice = (FoundChoice)stepContext.Result;
            var done = choice.Value == DoneOption;
            if (!done)
            {
                // If they chose a company, add it to the list.
                list.Add(choice.Value);
                
                
            }

            if (done || list.Count >= 3)
            {
                if (!done)
                {
                    var welcomeCard = CreateAdaptiveCardAttachment();
                    var response = CreateResponse(stepContext.Context.Activity,welcomeCard);
                    await stepContext.Context.SendActivityAsync(response, cancellationToken);
                }
               

                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync(list, cancellationToken);

            }
            else
            {
                // Otherwise, repeat this dialog, passing in the list from this iteration.
                return await stepContext.ReplaceDialogAsync(nameof(ReviewSelectionDialog), list, cancellationToken);
            }
        }
        private Activity CreateResponse(IActivity activity, Attachment attachment)
        {
            var response = ((Activity)activity).CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }
        private Activity CreateResponse(IActivity activity)
        {
            var response = ((Activity)activity).CreateReply();

            return response;
        }

        // Load attachment from file.
        private Attachment CreateAdaptiveCardAttachment()
        {
            // combine path for cross platform support
            string[] paths = { ".", "Cards", "endConversationCard.json" };
            string fullPath = Path.Combine(paths);
            var adaptiveCard = File.ReadAllText(fullPath,Encoding.UTF8);
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }
        /// <summary>
        /// Method to create the ticket if the end user give 3 no answers.
        /// And try get the mail from the end user to send the mail.
        /// </summary>
        /// <param name="stepContext">WaterfallStepContext</param>
        /// <param name="cancellationToken"></param>
        private void CreateTicketToSupport(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //Create Ticket
            try
            {


                var userProfile = (UserProfile)stepContext.Values[UserInfo];
                ConnectorClient connector = new ConnectorClient(new Uri(stepContext.Context.Activity.ServiceUrl), _configuration["MicrosoftAppId"], _configuration["MicrosoftAppPassword"]);
                var members = connector.Conversations.GetConversationMembersWithHttpMessagesAsync(stepContext.Context.Activity.Conversation.Id).Result.Body;
                var member = members?.Where(s => s.Name == stepContext.Context.Activity.From.Name).FirstOrDefault();
                if (member!=null&&member.Properties.ContainsKey("email"))
                {
                    userProfile.Mail = member.Properties["email"].ToString();
                }



                var question = userProfile.Questions.Last();
                var ticket = new Ticket();
                ticket.Question = question;
                ticket.UserName = userProfile.Name;
                ticket.Id = userProfile.TeamsId;
                ticket.UserTeamsId = userProfile.TeamsId;
                ticket.UserTeamsMail = userProfile.Mail;
                // call sync
                OpenTicketAsync(ticket, stepContext, cancellationToken);


            }
            catch (System.Exception error)
            {
                throw error;
            }
        }
        /// <summary>
        /// Open the ticket and send it to the flow, also fill the values in the TicketFlow model.
        /// If fails send a message to the user with the response of the error.
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="stepContext"></param>
        /// <param name="cancellationToken"></param>
        private async void  OpenTicketAsync(Ticket ticket, WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            try
            {
                if (!string.IsNullOrEmpty(ticket.Question) && !string.IsNullOrEmpty(ticket.UserTeamsMail))
                {
                    await SendMail(ticket, stepContext, cancellationToken);

                }
                else if (!string.IsNullOrEmpty(ticket.Question))
                {
                    //Use this mail adress mourao.martins@gmail.com if the user not have one
                    ticket.UserTeamsMail = "mourao.martins@gmail.com";
                    await SendMail(ticket, stepContext, cancellationToken);
                }

            }
            catch (Exception ex)
            {
                //to log this error
               // throw ex;
            }
          
           


        }
        /// <summary>
        /// Send mail to support get configuration from appsettings
        /// </summary>
        /// <param name="ticket">The model</param>
        /// <param name="stepContext">The  context to give feedback to the end user.</param>
        /// <param name="cancellationToken">The cancelation toke</param>
        /// <returns>Error exception or succes task</returns>
        private async Task SendMail(Ticket ticket, WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            try
            {

                var flowUrl = _configuration["FlowUrl"];
                var supportMail = _configuration["SupportMail"];
                string bodyMail = "<div>";
                bodyMail += "<p>Lamentamos não ter conseguido responder à sua dúvida. Solicito que contacte um dos meus colegas do apoio Service Desk através dos canais abaixo indicados:</p>";
                bodyMail += "<p>Portal Tutorial: Serviços/ Helpdesk – Portal de Serviços</p>";
                bodyMail += "<ul>";
                bodyMail += "<li>Email <a href='mailto:%20ServiceDesk@tutorial.com/'>ServiceDesk@tutorial.com</a></li>";
                bodyMail += "<li>Ext. xxxx</li>";
                bodyMail += "<li>Tel. (+xxx xxx xxx xxx) – Portugal</li>";
                bodyMail += "</ul>";
                bodyMail += "</div>";

                string subjectMail = "Help Desk - contactos de suporte.";
                var ticketFlow = new TicketFlow(ticket.UserTeamsMail, subjectMail, bodyMail);
                var httpClient = _httpClientFactory.CreateClient();
                var client = new HttpClient { BaseAddress = new Uri(flowUrl) };
                string output = Newtonsoft.Json.JsonConvert.SerializeObject(ticketFlow);
                var response = client.PostAsync(flowUrl, new StringContent(output, Encoding.UTF8, "application/json")).Result;

                if (response.IsSuccessStatusCode)
                {
                    Activity reply = MessageFactory.Text("I send you a email  the contacts of the suuport team .");
                    await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                }
                else
                {
                    throw new Exception(response.ToString());
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }
    }

   
}
