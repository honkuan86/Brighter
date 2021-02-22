﻿#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Serilog;

namespace GreetingsReceiverConsole
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    var subscriptions = new Subscription[]
                    {
                        new Subscription<GreetingEvent>(
                            new SubscriptionName("paramore.example.greeting"),
                            new ChannelName("greeting.event"),
                            new RoutingKey("greeting.event"),
                            timeoutInMilliseconds: 100)
                    };
                    //create the gateway

                    var consumerFactory = new KafkaMessageConsumerFactory(
                        new KafkaMessagingGatewayConfiguration {Name = "paramore.brighter", BootStrapServers = new[] {"localhost:9092"}},
                        new KafkaConsumerConfiguration
                        {
                            GroupId = "kafka-GreetingsReceiverConsole-Sample", OffsetDefault = AutoOffsetReset.Earliest, CommitBatchSize = 5
                        }
                    );

                    services.AddServiceActivator(options =>
                    {
                        options.Subscriptions = subscriptions;
                        options.ChannelFactory = new ChannelFactory(consumerFactory);
                        var outBox = new InMemoryOutbox();
                        options.BrighterMessaging = new BrighterMessaging()
                        {
                            OutBox = outBox,
                            Producer = new KafkaMessageProducerFactory(
                                new KafkaMessagingGatewayConfiguration
                                {
                                    Name = "paramore.brighter", 
                                    BootStrapServers = new[] {"localhost:9092"}
                                },
                                new KafkaMessagingProducerConfiguration
                                {
                                    MessageTimeoutMs = 500, 
                                    RequestTimeoutMs = 500
                                }
                            ).Create()
                        };
                    }).AutoFromAssemblies();


                    services.AddHostedService<ServiceActivatorHostedService>();
                })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }
    }
}
