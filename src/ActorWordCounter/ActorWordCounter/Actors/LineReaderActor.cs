using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Akka.Actor;
using ActorWordsCounter;
using ActorWordsCounter.Writes;

namespace ActorMapReduceWordCount.Actors
{
    public class LineReaderActor  : ReceiveActor
    {
        public static Props Create(IWriteStuff writer)
        {
            return Props.Create(() => new LineReaderActor(writer));
        }

        private readonly IWriteStuff _writer;

        public LineReaderActor(IWriteStuff writer)
        {
            _writer = writer;

            SetupBehaviors();
        }

        private void SetupBehaviors()
        {
            Receive<ReadLineForCounting>(msg =>
            {
                var cleanFileContents = Regex.Replace(msg.Line, @"[^\u0000-\u007F]", " ");

                var wordCounts = new Dictionary<String, Int32>();

                var wordArray = cleanFileContents.Split(new char[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in wordArray)
                {
                    if (wordCounts.ContainsKey(word))
                    {
                        wordCounts[word] += 1;
                    }
                    else
                    {
                        wordCounts.Add(word, 1);
                    }
                }

                Sender.Tell(new MappedList(msg.LineNumber, wordCounts));
            });

            Receive<Complete>(msg =>
            {
                Sender.Tell(msg);
            });
        }
    }
}
