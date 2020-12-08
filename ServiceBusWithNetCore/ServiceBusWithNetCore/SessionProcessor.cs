﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace ServiceBusWithNetCore
{
    class SessionProcessor
    {
        public static async Task RunSessionProcessor(string connectionString, string queueName, TimeSpan timeSpan)
        {
            // since ServiceBusClient implements IAsyncDisposable we create it with "await using"
            await using var client = new ServiceBusClient(connectionString);

            // get the options to use for configuring the processor
            var options = new ServiceBusSessionProcessorOptions
            {
                // By default after the message handler returns, the processor will complete the message
                // If I want more fine-grained control over settlement, I can set this to false.
                AutoCompleteMessages = false,

                // I can also allow for processing multiple sessions
                MaxConcurrentSessions = 5,

                // By default, there will be a single concurrent call per session. I can
                // increase that here to enable parallel processing within each session.
                MaxConcurrentCallsPerSession = 2
            };

            // create a processor that we can use to process the messages
            ServiceBusSessionProcessor processor = client.CreateSessionProcessor(queueName, options);

            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler;

            await processor.StartProcessingAsync();

            // since the message handler will run in a background thread, in order to prevent
            // this sample from terminating immediately
            DateTime endProcessing = DateTime.Now.Add(timeSpan);
            while (DateTime.Now < endProcessing)
                await Task.Delay(100);

            // stop processing once the task completion source was completed.
            await processor.StopProcessingAsync();
        }

        private static async Task MessageHandler(ProcessSessionMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Message received from session processor: {body}");

            // we can evaluate application logic and use that to determine how to settle the message.
            await args.CompleteMessageAsync(args.Message);

            // we can also set arbitrary session state using this receiver
            // the state is specific to the session, and not any particular message
            await args.SetSessionStateAsync(new BinaryData("some state"));
        }

        private static Task ErrorHandler(ProcessErrorEventArgs args)
        {
            // the error source tells me at what point in the processing an error occurred
            Console.WriteLine(args.ErrorSource);
            // the fully qualified namespace is available
            Console.WriteLine(args.FullyQualifiedNamespace);
            // as well as the entity path
            Console.WriteLine(args.EntityPath);
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }
    }
}
