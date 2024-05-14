using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wiadomosci;

namespace AbonentA
{
    class OdpA : IOdpA
    {
        public string kto { get; set; }
        public OdpA(string kto)
        {
            this.kto = kto;
        }
    }

    class Utils
    {
        public static string formatTimestamp(string input)
        {
            return "[" + input + "] ";
        }
    }

    class HandlerClass : IConsumer<Wiadomosci.IPubl>
    {
        public Task Consume(ConsumeContext<IPubl> ctx)
        {
            try
            {
                if (int.Parse(ctx.Message.tekst) % 2 == 0)
                {
                    ctx.RespondAsync<Wiadomosci.IOdpA>(new OdpA("abonent A"), responsectx =>
                    {
                        responsectx.Headers.Set("timestamp", DateTime.Now.ToString());
                        responsectx.Headers.Set("message_no", ctx.Message.tekst);
                    });
                }
            }
            catch (System.FormatException) { /* ignored */ }


            return Console.Out.WriteLineAsync(Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) + ctx.Message.tekst);
        }
    }

    internal class Program
    {
        public static Task HndlFault(ConsumeContext<Fault<IOdpA>> ctx)
        {
            foreach (var e in ctx.Message.Exceptions)
            {
                Console.Out.WriteLineAsync(
                    Utils.formatTimestamp(ctx.Headers.Get<string>("timestamp")) + 
                    "EXCEPTION: " + e.Message + " | " + ctx.Message.Message.kto);
            }
            return Task.CompletedTask;
        }
        static void DisplayStatus()
        {
            Console.Clear();
            Console.WriteLine("Receiver A initialized. Press ESC to quit");
        }

        static void Main(string[] args)
        {
            bool exitFlag = false;
            var instancja = new HandlerClass();

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc => {
                sbc.Host(new Uri("rabbitmq://cow.rmq2.cloudamqp.com/xgjwajpd"), h => {
                    h.Username("xgjwajpd");
                    h.Password("gMGsMovgDYfZHxL1F7ca2sjkY_zhWKiN");
                });
                sbc.ReceiveEndpoint("recvqueueA", ep => {
                    ep.Instance(instancja);
                    ep.Handler<Fault<IOdpA>>(HndlFault);
                });
            });
            bus.Start();

            DisplayStatus();

            while (!exitFlag)
            {
                var input = Console.ReadKey().Key;

                switch (input)
                {
                    case ConsoleKey.Escape:
                        exitFlag = true;
                        break;

                    default:
                        break;
                }

            }
            bus.Stop();
        }
    }
}
