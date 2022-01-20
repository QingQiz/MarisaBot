using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QQBot.EntityFrameworkCore.Entity.Audit
{
    public class AuditLogExternalInfo
    {
        /// <summary>
        ///  群号
        /// </summary>
        [MaxLength(12)]
        public string GroupId { get; set; }

        /// <summary>
        ///  群名
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        ///  对应用户在群里的权限
        /// </summary>
        public string GroupPermission { get; set; }

        /// <summary>
        ///  QQ号
        /// </summary>
        [MaxLength(12)]
        public string UserId { get; set; }

        /// <summary>
        ///  QQ昵称
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        ///  QQ备注
        /// </summary>
        public string UserAlias { get; set; }

        /// <summary>
        ///  涉及的消息内容
        /// </summary>
        public string Message { get; set; }
    }

    [Table("AuditLog")]
    public class AuditLog : AuditLogExternalInfo
    {
        [Key] public Guid EventId { get; set; }

        public string EventType { get; set; }

        public DateTime Time { get; set; }
    }
}