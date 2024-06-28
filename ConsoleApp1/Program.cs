// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using Newtonsoft.Json;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Images;
using System.ClientModel;
using System.Text.Json;

namespace Ola
{
    class TestClass
    {

        static string GetCurrentLocation()
        {
            // Call the location API here.
            return "San Francisco";
        }

        static string GetCurrentWeather(string location, string unit = "celsius")
        {
            // Call the weather API here.
            return $"31 {unit}";
        }

        static readonly ChatTool getCurrentLocationTool = ChatTool.CreateFunctionTool(
            functionName: nameof(GetCurrentLocation),
            functionDescription: "Get the user's current location"
        );

        static readonly ChatTool getCurrentWeatherTool = ChatTool.CreateFunctionTool(
            functionName: nameof(GetCurrentWeather),
            functionDescription: "Get the current weather in a given location",
            functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "The city and state, e.g. Boston, MA"
                },
                "unit": {
                    "type": "string",
                    "enum": [ "celsius", "fahrenheit" ],
                    "description": "The temperature unit to use. Infer this from the specified location."
                }
            },
            "required": [ "location" ]
        }
        """)
        );

        static async Task Main(string[] args)
        {

            var options = new string[] {
                "Chat-Example01_SimpleChat",
                "Chat-Example01_SimpleChatAsync",
                "Chat-Example03_FunctionCalling",
                "Chat-Example02_SimpleChatStreaming",
                "Chat-Generate-Embeddings",
                "Chat-Generate-Images",
                "Chat-Transcribe-Audio",
                "Chat-Compose1"
            };

            Console.WriteLine("informe a opção de chat?");
            var capturado = Console.ReadLine();
            while (!options.Any(x => x == capturado))
            {
                Console.WriteLine($@"Atenção opção inválida selecione uma das opções abaixo 
                                 {string.Join(",", options)}");

                Console.WriteLine("informe a opção de chat?");
                capturado = Console.ReadLine();

            }
            ChatClient client = new(model: "gpt-4o", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            #region " Chat " 
            if (capturado == "Chat-Example01_SimpleChat")
            {
                Console.WriteLine(@"------Simples envio de mensagens--------");
                Console.WriteLine("informe sua pergunta?");
                var question = Console.ReadLine();

                ChatCompletion completion = client.CompleteChat(question);
                Console.WriteLine($"[ASSISTANT]: {completion}");
            }
            else if (capturado == "Chat-Example01_SimpleChatAsync")
            {
                Console.WriteLine(@"------Simples envio de mensagens async--------");
                Console.WriteLine("informe sua pergunta?");
                var question = Console.ReadLine();

                ChatCompletion completion = await client.CompleteChatAsync("Say 'this is a test.'");
                Console.WriteLine($"[ASSISTANT]: {completion}");
            }
            else if (capturado == "Example02_SimpleChatStreaming")
            {
                Console.WriteLine(@"------Simples envio de mensagens que será respondida em blocos--------");
                Console.WriteLine("informe sua pergunta?");
                var question = Console.ReadLine();


                ResultCollection<StreamingChatCompletionUpdate> updates
                = client.CompleteChatStreaming(question);

                Console.WriteLine($"[ASSISTANT]:");
                var countBloco = 1;
                var countStreamingChatCompletionUpdate = 1;


                foreach (StreamingChatCompletionUpdate update in updates)
                {
                    countStreamingChatCompletionUpdate++;
                    Console.WriteLine($@" Bloco de Resposta StreamingChatCompletionUpdate [{countStreamingChatCompletionUpdate}] ");
                    Console.Write(@$"{JsonConvert.SerializeObject(update)}");

                    foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                    {
                        countBloco++;
                        Console.WriteLine($@" Bloco de Resposta ChatMessageContentPart [{countBloco}] ");
                        Console.Write(@$"{JsonConvert.SerializeObject(updatePart)}");
                    }
                }

            }
            else if (capturado == "Chat-Example03_FunctionCalling")
            {
                List<ChatMessage> messages = [
                            new UserChatMessage("What's the weather like today?"),
                ];

                ChatCompletionOptions opt = new()
                {
                    Tools = { getCurrentLocationTool, getCurrentWeatherTool },
                };

                bool requiresAction;

                do
                {
                    requiresAction = false;
                    ChatCompletion chatCompletion = client.CompleteChat(messages, opt);

                    switch (chatCompletion.FinishReason)
                    {
                        case ChatFinishReason.Stop:
                            {
                                // Add the assistant message to the conversation history.
                                messages.Add(new AssistantChatMessage(chatCompletion));
                                break;
                            }

                        case ChatFinishReason.ToolCalls:
                            {
                                // First, add the assistant message with tool calls to the conversation history.
                                messages.Add(new AssistantChatMessage(chatCompletion));

                                // Then, add a new tool message for each tool call that is resolved.
                                foreach (ChatToolCall toolCall in chatCompletion.ToolCalls)
                                {
                                    switch (toolCall.FunctionName)
                                    {
                                        case nameof(GetCurrentLocation):
                                            {
                                                string toolResult = GetCurrentLocation();
                                                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                                break;
                                            }

                                        case nameof(GetCurrentWeather):
                                            {
                                                // The arguments that the model wants to use to call the function are specified as a
                                                // stringified JSON object based on the schema defined in the tool definition. Note that
                                                // the model may hallucinate arguments too. Consequently, it is important to do the
                                                // appropriate parsing and validation before calling the function.
                                                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                                bool hasLocation = argumentsJson.RootElement.TryGetProperty("location", out JsonElement location);
                                                bool hasUnit = argumentsJson.RootElement.TryGetProperty("unit", out JsonElement unit);

                                                if (!hasLocation)
                                                {
                                                    throw new ArgumentNullException(nameof(location), "The location argument is required.");
                                                }

                                                string toolResult = hasUnit
                                                    ? GetCurrentWeather(location.GetString(), unit.GetString())
                                                    : GetCurrentWeather(location.GetString());
                                                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                                break;
                                            }

                                        default:
                                            {
                                                // Handle other unexpected calls.
                                                throw new NotImplementedException();
                                            }
                                    }
                                }

                                requiresAction = true;
                                break;
                            }

                        case ChatFinishReason.Length:
                            throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                        case ChatFinishReason.ContentFilter:
                            throw new NotImplementedException("Omitted content due to a content filter flag.");

                        case ChatFinishReason.FunctionCall:
                            throw new NotImplementedException("Deprecated in favor of tool calls.");

                        default:
                            throw new NotImplementedException(chatCompletion.FinishReason.ToString());
                    }
                    if (!requiresAction)
                        Console.Write(@$"{JsonConvert.SerializeObject(messages)}");
                } while (requiresAction);

            }
            else if (capturado == "Chat-Generate-Embeddings")
            {
                EmbeddingClient clientE = new(model: "text-embedding-3-small", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

                string description = "Best hotel in town if you like luxury hotels. They have an amazing infinity pool, a spa,"
                    + " and a really helpful concierge. The location is perfect -- right downtown, close to all the tourist"
                    + " attractions. We highly recommend this hotel.";
                EmbeddingGenerationOptions optionsE = new() { Dimensions = 512 };
                Embedding embedding = clientE.GenerateEmbedding(description, optionsE);
                ReadOnlyMemory<float> vector = embedding.Vector;
            }
            else if (capturado == "Chat-Generate-Images")
            {
                ImageClient clientimage = new(model: "dall-e-3", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                string prompt = "The concept for a living room that blends Scandinavian simplicity with Japanese minimalism for"
                                + " a serene and cozy atmosphere. It's a space that invites relaxation and mindfulness, with natural light"
                                + " and fresh air. Using neutral tones, including colors like white, beige, gray, and black, that create a"
                                + " sense of harmony. Featuring sleek wood furniture with clean lines and subtle curves to add warmth and"
                                + " elegance. Plants and flowers in ceramic pots adding color and life to a space. They can serve as focal"
                                + " points, creating a connection with nature. Soft textiles and cushions in organic fabrics adding comfort"
                                + " and softness to a space. They can serve as accents, adding contrast and texture.";

                ImageGenerationOptions optionsimage = new()
                {
                    Quality = GeneratedImageQuality.High,
                    Size = GeneratedImageSize.W1792xH1024,
                    Style = GeneratedImageStyle.Vivid,
                    ResponseFormat = GeneratedImageFormat.Bytes
                };

                GeneratedImage image = clientimage.GenerateImage(prompt, optionsimage);
                BinaryData bytes = image.ImageBytes;
                using FileStream stream = File.OpenWrite($"C:\\Users\\ogvieira\\Downloads\\{Guid.NewGuid()}.png");
                bytes.ToStream().CopyTo(stream);
            }
            else if (capturado == "Chat-Transcribe-Audio")
            {
                AudioClient clientAudio = new(model: "whisper-1", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

                string audioFilePath = Path.Combine("Assets", "audio_houseplant_care.mp3");

                AudioTranscriptionOptions optionsAudio = new()
                {
                    ResponseFormat = AudioTranscriptionFormat.Verbose,
                    Granularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
                };

                AudioTranscription transcription = clientAudio.TranscribeAudio($"C:\\Users\\ogvieira\\Downloads\\R.mp3", optionsAudio);

                Console.WriteLine("Transcription:");
                Console.WriteLine($"{transcription.Text}");

                Console.WriteLine();
                Console.WriteLine($"Words:");
                foreach (TranscribedWord word in transcription.Words)
                {
                    Console.WriteLine($"  {word.Word,15} : {word.Start.TotalMilliseconds,5:0} - {word.End.TotalMilliseconds,5:0}");
                }

                Console.WriteLine();
                Console.WriteLine($"Segments:");
                foreach (TranscribedSegment segment in transcription.Segments)
                {
                    Console.WriteLine($"  {segment.Text,90} : {segment.Start.TotalMilliseconds,5:0} - {segment.End.TotalMilliseconds,5:0}");
                }
            }
            else if (capturado == "Chat-Compose1")
            {

                AudioClient clientAudio = new(model: "whisper-1", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

                string audioFilePath = Path.Combine("Assets", "audio_houseplant_care.mp3");

                AudioTranscriptionOptions optionsAudio = new()
                {
                    ResponseFormat = AudioTranscriptionFormat.Verbose,
                    Granularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
                };

                AudioTranscription transcription = clientAudio.TranscribeAudio($"C:\\Users\\ogvieira\\Downloads\\audiogpt.mp3", optionsAudio);

                Console.WriteLine("Transcription:");
                Console.WriteLine($"{transcription.Text}");

                /**/
                List<ChatMessage> messages = [
                                new UserChatMessage(transcription.Text),
                ];

                ChatCompletionOptions opt = new()
                {
                    Tools = { getCurrentLocationTool, getCurrentWeatherTool },
                };

                bool requiresAction;

                do
                {
                    requiresAction = false;
                    ChatCompletion chatCompletion = client.CompleteChat(messages, opt);

                    switch (chatCompletion.FinishReason)
                    {
                        case ChatFinishReason.Stop:
                            {
                                // Add the assistant message to the conversation history.
                                messages.Add(new AssistantChatMessage(chatCompletion));
                                break;
                            }

                        case ChatFinishReason.ToolCalls:
                            {
                                // First, add the assistant message with tool calls to the conversation history.
                                messages.Add(new AssistantChatMessage(chatCompletion));

                                // Then, add a new tool message for each tool call that is resolved.
                                foreach (ChatToolCall toolCall in chatCompletion.ToolCalls)
                                {
                                    switch (toolCall.FunctionName)
                                    {
                                        case nameof(GetCurrentLocation):
                                            {
                                                string toolResult = GetCurrentLocation();
                                                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                                break;
                                            }

                                        case nameof(GetCurrentWeather):
                                            {
                                                // The arguments that the model wants to use to call the function are specified as a
                                                // stringified JSON object based on the schema defined in the tool definition. Note that
                                                // the model may hallucinate arguments too. Consequently, it is important to do the
                                                // appropriate parsing and validation before calling the function.
                                                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                                bool hasLocation = argumentsJson.RootElement.TryGetProperty("location", out JsonElement location);
                                                bool hasUnit = argumentsJson.RootElement.TryGetProperty("unit", out JsonElement unit);

                                                if (!hasLocation)
                                                {
                                                    throw new ArgumentNullException(nameof(location), "The location argument is required.");
                                                }

                                                string toolResult = hasUnit
                                                    ? GetCurrentWeather(location.GetString(), unit.GetString())
                                                    : GetCurrentWeather(location.GetString());
                                                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                                break;
                                            }

                                        default:
                                            {
                                                // Handle other unexpected calls.
                                                throw new NotImplementedException();
                                            }
                                    }
                                }

                                requiresAction = true;
                                break;
                            }

                        case ChatFinishReason.Length:
                            throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                        case ChatFinishReason.ContentFilter:
                            throw new NotImplementedException("Omitted content due to a content filter flag.");

                        case ChatFinishReason.FunctionCall:
                            throw new NotImplementedException("Deprecated in favor of tool calls.");

                        default:
                            throw new NotImplementedException(chatCompletion.FinishReason.ToString());
                    }
                    if (!requiresAction)
                        Console.Write(@$"{JsonConvert.SerializeObject(messages)}");
                } while (requiresAction);

            }
            #endregion


        }
    }

}
