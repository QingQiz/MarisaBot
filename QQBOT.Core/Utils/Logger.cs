using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Audit.Core;
using Mirai_CSharp.Models;
using QQBOT.EntityFrameworkCore;
using QQBOT.EntityFrameworkCore.Entity.Audit;

namespace QQBOT.Core.Utils
{
    public static class Logger
    {
        private static BotDbContext _dbContext;

        static Logger()
        {
            _dbContext = new BotDbContext();
        }

        private static async Task Log(string type, AuditLogExternalInfo obj)
        {
            if (_dbContext.Messages.Any(m => m.MessageId == obj.MessageId)) return;

            // print max 150 chars
            Console.WriteLine(
                $@"[{DateTime.Now:MM-dd hh:mm:ss}][{type}]: {obj.Message[..Math.Min(obj.Message.Length, 150)]}");
            // log to db
            await AuditScope.LogAsync(type, obj);
        }

        private static string GetMessage(IMessageBase[] chain)
        {
            return string.Join(' ', chain[1..] as IEnumerable<IMessageBase>);
        }

        public static async Task FriendMessage(IFriendMessageEventArgs args)
        {
            var obj = new AuditLogExternalInfo
            {
                UserId = args.Sender.Id.ToString(),
                UserName = args.Sender.Name,
                UserAlias = args.Sender.Remark,
                Message = GetMessage(args.Chain),
                MessageId = args.Chain[0].ToString()
            };
            await Log("Receive.Friend", obj);
        }

        public static async Task GroupMessage(IGroupMessageEventArgs args)
        {
            var obj = new AuditLogExternalInfo
            {
                UserId = args.Sender.Id.ToString(),
                UserAlias = args.Sender.Name,
                GroupId = args.Sender.Group.Id.ToString(),
                GroupName = args.Sender.Group.Name,
                GroupPermission = args.Sender.Group.Permission.ToString(),
                Message = GetMessage(args.Chain),
                MessageId = args.Chain[0].ToString()
            };
            await Log("Receive.Group", obj);
        }

        public static async Task TempMessage(ITempMessageEventArgs args)
        {
            var obj = new AuditLogExternalInfo
            {
                UserId = args.Sender.Id.ToString(),
                UserAlias = args.Sender.Name,
                GroupId = args.Sender.Group.Id.ToString(),
                GroupName = args.Sender.Group.Name,
                GroupPermission = args.Sender.Group.Permission.ToString(),
                MessageId = args.Chain[0].ToString(),
                Message = GetMessage(args.Chain)
            };
            await Log("Receive.Temp", obj);
        }

        public static void UnknownMessage(IUnknownMessageEventArgs args)
        {
            Console.WriteLine($@"[{DateTime.Now:MM-dd hh:mm:ss}][Receive.Unknown]: {args.Message.ToString()}");
        }
    }
}