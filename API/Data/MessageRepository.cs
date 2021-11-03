using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        public MessageRepository(DataContext context, IMapper mapper)
        {
            _mapper = mapper;
            _context = context;
        }

        public void AddMessage(Message message)
        {
            _context.Messages.Add(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messages.Remove(message);
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages.FindAsync(id);
        }

        //MessageParams are like PaginationParams, except they also contain the current user's username, as well as which "container" (inbox v outbox) that we'd like to view
        public async Task<PagedList<MessageDTO>> GetMessagesForUser(MessageParams messageParams)
        {
            var query = _context.Messages
                .OrderByDescending(m => m.MessageSent)
                .AsQueryable();

            //Potentially change this logic to work like Twitter DMs, where you can see all threads at once but had an Unread notification
            //This currently checks the container string to swap which types of messages we're viewing: incoming or outgoing
            query = messageParams.Container switch
            {
                "Inbox" => query.Where(u => u.Recipient.UserName == messageParams.Username),
                "Outbox" => query.Where(u => u.Sender.UserName == messageParams.Username),
                _ => query.Where(u => u.Recipient.UserName == messageParams.Username && u.DateRead == null)
            };

            var messages = query.ProjectTo<MessageDTO>(_mapper.ConfigurationProvider);

            return await PagedList<MessageDTO>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<MessageDTO>> GetMessageThread(string currentUsername, string recipientUsername)
        {
            //LINQ Query to find all messages either sent by the current user to a specific user OR send by that specific user to the current user.
            //Will basically give us entire convo history between two people, when ordered correctly (order by MessageSent date)
            //Also uses Include to get the Users associated to each message, and ThenInclude to grab those Users' photos
            var messages = await _context.Messages
                .Include(u => u.Sender).ThenInclude(p => p.Photos)
                .Include(u => u.Recipient).ThenInclude(p => p.Photos)
                .Where(m => m.Recipient.UserName.ToLower() == currentUsername.ToLower()
                        && m.Sender.UserName.ToLower() == recipientUsername.ToLower()
                        || m.Recipient.UserName.ToLower() == recipientUsername.ToLower()
                        && m.Sender.UserName.ToLower() == currentUsername.ToLower()
                )
                .OrderBy(m => m.MessageSent)
                .ToListAsync();

            //Grabs all messages in this thread that are marked unread, and have been sent to the current user (so we can mark them as read)
            var unreadMessages = messages.Where(m => m.DateRead == null && m.Recipient.UserName == currentUsername).ToList();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.DateRead = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            //Returns MessageDTOs to client
            return _mapper.Map<IEnumerable<MessageDTO>>(messages);
        }

        public async Task<bool> SaveallAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}