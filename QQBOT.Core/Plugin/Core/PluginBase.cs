using System;
using System.Linq;
using System.Threading.Tasks;
using Audit.Core;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using Mirai_CSharp.Plugin.Interfaces;
using QQBOT.Core.Attribute;
using QQBOT.Core.Utils;
using QQBOT.EntityFrameworkCore.Entity.Audit;

namespace QQBOT.Core.Plugin.Core
{
    [MiraiPlugin(null)]
    [MiraiPluginDisabled]
    public abstract class PluginBase : IFriendMessage, IGroupMessage, ITempMessage, IUnknownMessage
    {
        #region Handler

        protected abstract Task FriendMessageHandler(MiraiHttpSession session, IFriendInfo sender, string message);

        protected abstract Task GroupMessageHandler(MiraiHttpSession session, IGroupMemberInfo sender, string message);

        protected virtual async Task TempMessageHandler(MiraiHttpSession session, IGroupMemberInfo sender, string message)
        {
            await GroupMessageHandler(session, sender, message);
        }

        protected virtual void UnknownMessageHandler(MiraiHttpSession session,
            IUnknownMessageEventArgs args)
        {
        }

        #endregion

        #region Logger

        private async Task Log(string type, AuditLogExternalInfo obj)
        {
            // print max 150 chars
            Console.WriteLine(
                $@"[{DateTime.Now:MM-dd hh:mm:ss}][{type}][{GetType().Name}]: {obj.Message[..Math.Min(obj.Message.Length, 150)]}");
            // log to db
            await AuditScope.LogAsync(type, obj);
        }

        #endregion

        #region Handler Wrapper

        public async Task<bool> FriendMessage(MiraiHttpSession session, IFriendMessageEventArgs e)
        {
            var attribute = GetType().GetCustomAttributes(typeof(MiraiPluginAttribute), true).First() as MiraiPluginAttribute;
            var commandPrefix = attribute?.CommandPrefix ?? "";
            
            if (!e.Chain.BeginWith(commandPrefix)) return false;

            try
            {
                await FriendMessageHandler(session, e.Sender, e.Chain.GetArguments(commandPrefix));
            }
            catch (NotImplementedException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                Console.WriteLine(exception.StackTrace);
                throw;
            }

            var obj = new AuditLogExternalInfo
            {
                UserId    = e.Sender.Id.ToString(),
                UserName  = e.Sender.Name,
                UserAlias = e.Sender.Remark,
                Message   = e.Chain.GetMessage(),
                MessageId = e.Chain.GetMessageId(),
                HandledBy = GetType().Name
            };

            await Log("Receive.Friend", obj);
            return true;
        }

        public async Task<bool> GroupMessage(MiraiHttpSession session, IGroupMessageEventArgs e)
        {
            var attribute = GetType().GetCustomAttributes(typeof(MiraiPluginAttribute), true).First() as MiraiPluginAttribute;
            var commandPrefix = attribute?.CommandPrefix ?? "";
            
            if (!e.Chain.BeginWith(commandPrefix)) return false;

            try
            {
                await GroupMessageHandler(session, e.Sender, e.Chain.GetArguments(commandPrefix));
            }
            catch (NotImplementedException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                Console.WriteLine(exception.StackTrace);
                throw;
            }

            var obj = new AuditLogExternalInfo
            {
                UserId          = e.Sender.Id.ToString(),
                UserAlias       = e.Sender.Name,
                GroupId         = e.Sender.Group.Id.ToString(),
                GroupName       = e.Sender.Group.Name,
                GroupPermission = e.Sender.Group.Permission.ToString(),
                Message         = e.Chain.GetMessage(),
                MessageId       = e.Chain.GetMessageId(),
                HandledBy       = GetType().Name
            };

            await Log("Receive.Group", obj);
            return true;
        }

        public async Task<bool> TempMessage(MiraiHttpSession session, ITempMessageEventArgs e)
        {
            var attribute = GetType().GetCustomAttributes(typeof(MiraiPluginAttribute), true).First() as MiraiPluginAttribute;
            var commandPrefix = attribute?.CommandPrefix;
            
            if (!e.Chain.BeginWith(commandPrefix)) return false;

            try
            {
                await TempMessageHandler(session, e.Sender, e.Chain.GetArguments(commandPrefix));
            }
            catch (NotImplementedException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                Console.WriteLine(exception.StackTrace);
                throw;
            }

            var obj = new AuditLogExternalInfo
            {
                UserId          = e.Sender.Id.ToString(),
                UserAlias       = e.Sender.Name,
                GroupId         = e.Sender.Group.Id.ToString(),
                GroupName       = e.Sender.Group.Name,
                GroupPermission = e.Sender.Group.Permission.ToString(),
                Message         = e.Chain.GetMessage(),
                MessageId       = e.Chain.GetMessageId(),
                HandledBy       = GetType().Name
            };

            await Log("Receive.Temp", obj);
            return true;
        }

        public async Task<bool> UnknownMessage(MiraiHttpSession session, IUnknownMessageEventArgs e)
        {
            Console.WriteLine($@"[{DateTime.Now:MM-dd hh:mm:ss}][{GetType().Name}][Receive.Unknown]: {e.Message.ToString()}");
            return await Task.FromResult(true);
        }

        #endregion
    }
}