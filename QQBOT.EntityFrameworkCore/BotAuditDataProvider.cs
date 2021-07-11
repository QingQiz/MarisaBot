using System;
using System.Threading.Tasks;
using Audit.Core;
using QQBOT.EntityFrameworkCore.Entity.Audit;
using QQBOT.EntityFrameworkCore.Entity.Common;

namespace QQBOT.EntityFrameworkCore
{
    public class BotAuditDataProvider : AuditDataProvider
    {
        public override object InsertEvent(AuditEvent auditEvent)
        {
            return InsertEventAsync(auditEvent).Result;
        }

        public override async Task<object> InsertEventAsync(AuditEvent auditEvent)
        {
            await using var db = new BotDbContext();

            var message = auditEvent.CustomFields["Message"]?.ToString();
            
            var messageId = auditEvent.CustomFields["MessageId"]?.ToString();

            var log = new AuditLog
            {
                EventId   = new Guid(),
                EventType = auditEvent.EventType,
                StartDate = auditEvent.StartDate,
                EndDate   = auditEvent.EndDate,
                Duration  = auditEvent.Duration,
                GroupId   = auditEvent.CustomFields["GroupId"]?.ToString(),
                GroupName = auditEvent.CustomFields["GroupName"]?.ToString(),
                UserId    = auditEvent.CustomFields["UserId"]?.ToString(),
                UserName  = auditEvent.CustomFields["UserName"]?.ToString(),
                UserAlias = auditEvent.CustomFields["UserAlias"]?.ToString(),
                Message   = message?[..Math.Min(message.Length, 150)],
                MessageId = messageId
            };
            await db.Logs.AddAsync(log);

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                await db.Messages.AddAsync(new QMessage
                {
                    Message   = message,
                    MessageId = messageId,
                    Type      = auditEvent.EventType,
                });
            }

            await db.SaveChangesAsync();
            return log.EventId;
        }
    }
}