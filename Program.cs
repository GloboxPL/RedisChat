using System;
using System.Linq;
using StackExchange.Redis;

namespace RedisChat
{
    class Program
    {
        private static ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
        private static IDatabase db = redis.GetDatabase();
        private static ISubscriber pubsub = redis.GetSubscriber();
        private static readonly string[] channels = { "allchat", "en-trading-1", "en-trading-2", "pl-grouping-1", "pl-grouping-2" };
        private static int current;
        private static string userName;
        private static string friendsKey = "friends:";

        static void Main(string[] args)
        {
            Initialize();
            while (true)
            {
                Write();
            }
        }

        private static void Initialize()
        {
            Console.Write("Enter your name: ");
            userName = Console.ReadLine();
            friendsKey += userName;
            RunChannelOperation("allchat", SubscribeChannel);
            Console.WriteLine("Commands: friends: | addfriend:name | onchan:channel | schan:channel | pschan:pattern | uchan:channel | wchan:channel | cls: | exit:");
        }

        private static void Write()
        {
            string text = Console.ReadLine();
            if (!IsCommand(text))
            {
                string message = $"{userName}: {text}  " + $"({DateTime.Now.Hour}:{DateTime.Now.Minute} on {channels[current]})";
                pubsub.Publish(channels[current], message);
            }
        }

        private static void SubscribeChannel(int i)
        {
            pubsub.Subscribe(channels[i], (channel, message) => MessageAction(message));
            db.SetAdd(channels[i], userName);
            Console.WriteLine("You joined to channel {0}.", channels[i]);
        }

        private static void UnsubscribeChannel(int i)
        {
            pubsub.Unsubscribe(channels[i]);
            db.SetRemove(channels[i], userName);
            Console.WriteLine("You left channel {0}.", channels[i]);
        }

        private static void ChooseChannelToWrite(int i)
        {
            current = i;
            Console.WriteLine("Now you are writing to {0}", channels[current]);
        }

        private static void RunChannelOperation(string channelName, Action<int> action)
        {
            if (!channels.Contains(channelName))
            {
                Console.WriteLine("Not found.");
                return;
            }
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i] == channelName)
                {
                    action.Invoke(i);
                    return;
                }
            }
        }

        private static void PatternSubscribeChannel(string pattern)
        {
            int count = 0;
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i].Contains(pattern))
                {
                    SubscribeChannel(i);
                    count++;
                }
            }
            if (count == 0)
            {
                Console.WriteLine("No matching channel was found.");
            }
        }

        private static void GetFriends()
        {
            RedisKey[] keys = new RedisKey[channels.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new RedisKey(channels[i]);
            }
            string allOnlineUsersKey = "temp";
            db.SetAdd(allOnlineUsersKey, db.SetCombine(SetOperation.Union, keys));
            db.SetRemove(allOnlineUsersKey, userName);
            var friendsOffline = db.SetCombine(SetOperation.Difference, friendsKey, allOnlineUsersKey);
            var friendsOnline = db.SetCombine(SetOperation.Intersect, friendsKey, allOnlineUsersKey);
            db.KeyDelete(allOnlineUsersKey);
            Console.Write("{0} friends online: ", friendsOnline.Length);
            foreach (var friend in friendsOnline)
            {
                Console.Write(friend + " | ");
            }
            Console.WriteLine();
            Console.Write("Offline friends {0}: ", friendsOffline.Length);
            foreach (var friend in friendsOffline)
            {
                Console.Write(friend + " | ");
            }
            Console.WriteLine();
        }

        private static void GetUsersOnChannel(int i)
        {
            var users = db.SetMembers(channels[i]);
            Console.Write("Users on this channel {0}: ", users.Length);
            foreach (var user in users)
            {
                Console.Write(user + " | ");
            }
            Console.WriteLine();
        }

        private static void AddFriend(string friendName)
        {
            db.SetAdd(friendsKey, friendName);
            Console.WriteLine("{0} was added sucessfuly.", friendName);
        }

        private static bool IsCommand(string text)
        {
            var command = text.Split(':');
            if (command.Length != 2) return false;
            switch (command[0])
            {
                case "friends":
                    GetFriends();
                    return true;
                case "addfriend":
                    AddFriend(command[1]);
                    return true;
                case "onchan":
                    RunChannelOperation(command[1], GetUsersOnChannel);
                    return true;
                case "schan":
                    RunChannelOperation(command[1], SubscribeChannel);
                    return true;
                case "pschan":
                    PatternSubscribeChannel(command[1]);
                    return true;
                case "uchan":
                    RunChannelOperation(command[1], UnsubscribeChannel);
                    return true;
                case "wchan":
                    RunChannelOperation(command[1], ChooseChannelToWrite);
                    return true;
                case "cls":
                    Console.Clear();
                    return true;
                case "exit":
                    Exit();
                    return true;
                default:
                    return false;
            }
        }

        private static void Exit()
        {
            pubsub.UnsubscribeAll();
            foreach (var channel in channels)
            {
                db.SetRemove(channel, userName);
            }
            Environment.Exit(0);
        }

        private static void MessageAction(RedisValue message)
        {
            int initialCursorTop = Console.CursorTop;
            int initialCursorLeft = Console.CursorLeft;

            Console.MoveBufferArea(0, initialCursorTop, Console.WindowWidth, 1, 0, initialCursorTop + 1);
            Console.CursorTop = initialCursorTop;
            Console.CursorLeft = 0;

            Console.WriteLine(message);

            Console.CursorTop = initialCursorTop + 1;
            Console.CursorLeft = initialCursorLeft;
        }
    }
}
