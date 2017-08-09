﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Miki.Models
{
    // TODO: add more event types
    public enum EventMessageType
    {
        JOINSERVER = 0,
        LEAVESERVER = 1,
    }

    [Table("EventMessages")]
    public class EventMessage
    {
        [Key]
        [Column("ChannelId", Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long ChannelId { get; set; }

        [Key]
        [Column("EventType", Order = 1)]
        public short EventType { get; set; }

        [Column("Message")]
        public string Message { get; set; }
    }
}