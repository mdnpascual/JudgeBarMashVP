using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.Chat;
using OpenAI;
using System.IO;

namespace JudgeBarMashVP
{
    internal class AI_OCR
    {
        string model = "google/gemma-3-12b";
        string serverUrl = "http://127.0.0.1:1234/v1";
        string apiKey = "not_needed_for_lmstudio";
        OpenAIClient client;
        ChatClient chatClient;

        public AI_OCR()
        {
            this.client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
            {
                Endpoint = new Uri(serverUrl)
            });
            this.chatClient = client.GetChatClient(model);
        }

        public string PredictNumber(Bitmap bitmap)
        {
            // Convert Bitmap to BinaryData
            using (var memoryStream = new MemoryStream())
            {
                // Save the bitmap to the memory stream in PNG format
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0; // Reset the stream position to the beginning

                // Create BinaryData from the stream
                BinaryData imageBytes = BinaryData.FromStream(memoryStream);

                // Create the user chat message with the image part
                var userMessage = new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart("You job is to extract numbers from the images I provide you. \n" +
                    "You should only extract the number if you see the word 'ms' at the bottom of the image.\n " +
                    "If you recognize `ms` the number should be at the top of it. \n " +
                    "either a minus sign on the left of the image or a plus sign of the image. \n" +
                    "if you see the number AND a minus sign on the left, return me the number in negative form. else just the number. \n" +
                    "If you do not find any numbers or the 'ms' marker, return an empty string without any additional text." +
                    "Someone will kill the innocent kittens if you don't extract the text exactly. So, make sure you extract every bit of the text."),
                    ChatMessageContentPart.CreateImagePart(imageBytes, "image/png")
                );

                // Send the message
                var response = chatClient.CompleteChat(new[] { userMessage });

                var extractedResponse = response.Value.Content[0].Text;

                // Show the returned value
                Console.WriteLine(extractedResponse);
                return extractedResponse;
            }
        }
    }
}
