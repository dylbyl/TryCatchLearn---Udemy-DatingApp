

using System;

namespace API.DTOs
{
    public class MessageDTO
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderUsername { get; set; }
        public string SenderPhotoURL { get; set; }
        public int RecipientId { get; set; }
        public string RecipientUsername { get; set; }
        public string RecipientPhotoURL { get; set; }

        public string Content { get; set; }
        public System.DateTime? DateRead { get; set; }
        public DateTime MessageSent { get; set; }
    }
}
