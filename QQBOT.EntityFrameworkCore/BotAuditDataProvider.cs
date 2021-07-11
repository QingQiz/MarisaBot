using System;
using System.Threading.Tasks;
using Audit.Core;
using QQBOT.EntityFrameworkCore.Entity.Audit;

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
                Message   = auditEvent.CustomFields["Message"]?.ToString()
            };

            await db.Logs.AddAsync(log);
            await db.SaveChangesAsync();

            return log.EventId;
        }
    }
}