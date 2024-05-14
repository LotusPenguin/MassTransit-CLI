using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Serialization;
using Wiadomosci;

namespace Wydawca
{
    class Publ : IPubl
    {
        public string tekst { get; set; }

        public Publ() { }

        public Publ(string tekst)
        {
            this.tekst = tekst;
        }
    }

    class Klucz : SymmetricKey
    {
        public byte[] Key { get; set; }

        public byte[] IV { get; set; }
    }

    public class Dostawca : ISymmetricKeyProvider
    {
        private string k;
        public Dostawca(string k) { this.k = k; }
        public bool TryGetKey(string id, out SymmetricKey key)
        {
            var sk = new Klucz();
            sk.IV = Encoding.ASCII.GetBytes(id.Substring(0, 16));
            sk.Key = Encoding.ASCII.GetBytes(k);
            key = sk;
            return true;
        }
    }

    class Utils
    {
        public static string formatTimestamp(string input)
        {
            return "[" + input + "] ";
        }
    }

    class ControllerHandlerClass : IConsumer<Wiadomosci.IControllerRequest>
    {
        public bool dziala = false;

        public Task Consume(ConsumeContext<IControllerRequest> ctx)
        {
            dziala = ctx.Message.dziala;
            if (dziala)
            {
                return Console.Out.WriteLineAsync(Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) + "Generator started by the controller");
            }
            else
            {
                return Console.Out.WriteLineAsync(Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) + "Generator stopped by the controller");
            }

        }
    }

    class HandlerClass : IConsumer<Wiadomosci.IOdpA>, IConsumer<Wiadomosci.IOdpB>
    {
        static Random rnd = new Random();
        ISendEndpoint sendEpA;
        ISendEndpoint sendEpB;

        public HandlerClass()
        {
            var busA = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h =>
                {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
            });
            var tsk = busA.GetSendEndpoint(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd/recvqueueA"));
            tsk.Wait();
            var sendEpA = tsk.Result;
            this.sendEpA = sendEpA;

            var busB = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h =>
                {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
            });
            tsk = busB.GetSendEndpoint(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd/recvqueueB"));
            tsk.Wait();
            var sendEpB = tsk.Result;
            this.sendEpB = sendEpB;
        }

        public Task Consume(ConsumeContext<IOdpA> ctx)
        {
            int randInt = rnd.Next(1, 4);
            if (randInt == 3)
            {
                try
                {
                    throw new Exception("Message processing exception");
                }
                catch (Exception e)
                {
                    Console.Out.WriteLineAsync(
                    Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) +
                    "Throwing Exception to OdpA" + $" (#{ctx.Headers.Get<string>("message_no")})");

                    //Message, gdy wywołany wyjątek od abonenta A
                    sendEpA.Send(new Publ("Message from A processing exception, retrying..."), responsectx =>
                    {
                        responsectx.Headers.Set("timestamp", DateTime.Now.ToString());
                    });

                    return Task.FromException(e);
                }
            }
            else
            {
                return Console.Out.WriteLineAsync(
                    Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) +
                    "#" + ctx.Headers.Get<string>("message_no") + " " +
                    ctx.Message.kto);
            }
        }

        public Task Consume(ConsumeContext<IOdpB> ctx)
        {
            int randInt = rnd.Next(1, 4);
            if (randInt == 3)
            {
                try
                {
                    throw new Exception("Message processing exception");
                }
                catch (Exception e)
                {
                    Console.Out.WriteLineAsync(
                    Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) +
                    "Throwing Exception to OdpB" + $" (#{ctx.Headers.Get<string>("message_no")})");

                    //Message, gdy wywołany wyjątek od abonenta B
                    sendEpB.Send<Wiadomosci.IPubl>(new Publ("Message from B processing exception, retrying..."), responsectx =>
                    {
                        responsectx.Headers.Set("timestamp", DateTime.Now.ToString());
                    });

                    return Task.FromException(e);
                }
            }
            else
            {
                return Console.Out.WriteLineAsync(
                    Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) +
                    "#" + ctx.Headers.Get<string>("message_no") + " " +
                    ctx.Message.kto);
            }
        }
    }

    class PublishObserver : IPublishObserver
    {
        private static readonly object DictionaryAccessLock = new object();

        public Dictionary<string, Dictionary<string, int>> statistics;
        public PublishObserver() 
        {
            statistics = new Dictionary<string, Dictionary<string, int>>
            {
                {
                    "Tries",
                    new Dictionary<string, int>()
                },
                {
                    "Successes",
                    new Dictionary<string, int>()
                }

            };
        }

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            string messageType = typeof(T).Name;
            lock(DictionaryAccessLock)
            {
                if (statistics["Successes"].ContainsKey(messageType))
                {
                    statistics["Successes"][messageType] += 1;
                }
                else
                {
                    statistics["Successes"][messageType] = 1;
                }
            }
            return Task.CompletedTask;
        }

        public Task PrePublish<T>(PublishContext<T> context) where T : class
        {
            string messageType = typeof(T).Name;
            lock (DictionaryAccessLock)
            {
                if (statistics["Tries"].ContainsKey(messageType))
                {
                    statistics["Tries"][messageType] += 1;
                }
                else
                {
                    statistics["Tries"][messageType] = 1;
                }
            }
            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class
        {
            return Task.CompletedTask;
        }
    }

    class ConsumeObserver : IConsumeObserver
    {
        private static readonly object DictionaryAccessLock = new object();
        public Dictionary<string, Dictionary<string, int>> statistics;
        public ConsumeObserver() 
        {
            statistics = new Dictionary<string, Dictionary<string, int>>
            {
                {
                    "Tries",
                    new Dictionary<string, int>()
                },
                {
                    "Successes",
                    new Dictionary<string, int>()
                }
                
            };
        }

        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
        {
            return Task.CompletedTask;
        }

        public Task PostConsume<T>(ConsumeContext<T> context) where T : class
        {
            string messageType = typeof(T).Name;
            lock(DictionaryAccessLock)
            {
                if (statistics["Successes"].ContainsKey(messageType))
                {
                    statistics["Successes"][messageType] += 1;
                }
                else
                {
                    statistics["Successes"][messageType] = 1;
                }
            }
            return Task.CompletedTask;
        }

        public Task PreConsume<T>(ConsumeContext<T> context) where T : class
        {
            string messageType = typeof(T).Name;
            lock(DictionaryAccessLock)
            {
                if (statistics["Tries"].ContainsKey(messageType))
                {
                    statistics["Tries"][messageType] += 1;
                }
                else
                {
                    statistics["Tries"][messageType] = 1;
                }
            }
            return Task.CompletedTask;
        }
    }

    internal class Program
    {
        private static readonly object ConsoleWriterLock = new object();
        static void DisplayStatus()
        {
            Console.Clear();
            Console.WriteLine("Publisher initialized. Press ESC to quit");
        }

        static void PrintStatistics(ConsumeObserver consumeObserver, PublishObserver publishObserver)
        {
            Console.Out.WriteLineAsync();
            Console.Out.WriteLineAsync("###### MESSAGE HANDLING ######");
            foreach (var label in consumeObserver.statistics.Keys)
            {
                Console.Out.WriteLineAsync("### " + label.ToString() + " ###");
                foreach (var pair in consumeObserver.statistics[label])
                {
                    Console.Out.WriteAsync(pair.Key.ToString() + ":\t" + pair.Value.ToString() + "\t");
                }
                Console.Out.WriteLineAsync();
            }
            Console.Out.WriteLineAsync();

            Console.Out.WriteLineAsync();
            Console.Out.WriteLineAsync("###### MESSAGE PUBLISHING ######");
            foreach (var label in publishObserver.statistics.Keys)
            {
                Console.Out.WriteLineAsync("### " + label.ToString() + " ###");
                foreach (var pair in publishObserver.statistics[label])
                {
                    Console.Out.WriteAsync(pair.Key.ToString() + ":\t" + pair.Value.ToString() + "\t");
                }
                Console.Out.WriteLineAsync();
            }
            Console.Out.WriteLineAsync();
            Console.Out.WriteLineAsync();
        }

        static void Main(string[] args)
        {
            bool exitFlag = false;
            int counter = 0;

            ControllerHandlerClass controllerHandler = new ControllerHandlerClass();
            HandlerClass publisherHandler = new HandlerClass();
            ConsumeObserver consumeObserver = new ConsumeObserver();
            PublishObserver publishObserver = new PublishObserver();

            var controllerBus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h =>
                {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
                sbc.ReceiveEndpoint("controllerqueue", ep =>
                {
                    ep.Instance(controllerHandler);
                    ep.UseMessageRetry(r =>
                    {
                        r.Immediate(5);
                    });
                });
                sbc.UseEncryptedSerializer(
                    new AesCryptoStreamProvider(
                        new Dostawca("18466318466318466318466318466318"),
                            "0123456789abcdef"));
            });
            controllerBus.ConnectConsumeObserver(consumeObserver);
            controllerBus.ConnectPublishObserver(publishObserver);

            var publisherBus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h =>
                {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
                sbc.ReceiveEndpoint("publisherqueue", ep =>
                {
                    ep.Instance(publisherHandler);
                    ep.UseMessageRetry(r =>
                    {
                        r.Immediate(5);
                    });
                });
            });
            publisherBus.ConnectConsumeObserver(consumeObserver);
            publisherBus.ConnectPublishObserver(publishObserver);

            controllerBus.Start();
            publisherBus.Start();

            //Keyboard input resoultion task
            Task.Factory.StartNew(() =>
            {
                ConsoleKey consoleKey = Console.ReadKey().Key;
                while (consoleKey != ConsoleKey.Escape)
                {
                    switch (consoleKey)
                    {
                        case ConsoleKey.S:
                            lock(ConsoleWriterLock)
                            {
                                PrintStatistics(consumeObserver, publishObserver);
                            }
                            break;
                        default:
                            break;
                    }

                    consoleKey = Console.ReadKey().Key;
                }
                exitFlag = true;
            });

            DisplayStatus();

            //Main loop
            while (!exitFlag)
            {
                if (controllerHandler.dziala)
                {
                    counter++;
                    publisherBus.Publish(new Publ() { tekst = counter.ToString() }, ctx =>
                    {
                        ctx.Headers.Set("timestamp", DateTime.Now.ToString());
                    });
                    Thread.Sleep(1000);
                }
                else
                {
                    //busy waiting
                    Thread.Sleep(100);
                }
            }

            controllerBus.Stop();
            publisherBus.Stop();
        }
    }
}
