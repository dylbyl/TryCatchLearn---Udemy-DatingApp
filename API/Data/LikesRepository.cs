using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class LikesRepository : ILikesRepository
    {
        private readonly DataContext _context;
        public LikesRepository(DataContext context)
        {
            _context = context;
        }

        //Check ILikesRepository for information on what these methods do

        public async Task<UserLike> GetUserLike(int sourceUserId, int likedUserId)
        {
            return await _context.Likes.FindAsync(sourceUserId, likedUserId);
        }

        public async Task<PagedList<LikeDTO>> GetUserLikes(LikesParams likesParams)
        {
            var users = _context.Users.OrderBy(u => u.UserName).AsQueryable();
            var likes = _context.Likes.AsQueryable();

            //Gets the users that the currently logged-in user has LIKED
            if (likesParams.predicate == "liked")
            {
                //Does not run yet - simply adds on to the queries
                //Will look for all likes where the current user is the SourceUser
                likes = likes.Where(like => like.SourceUserId == likesParams.UserId);
                //Filters out the Users query to only include Users that the current user likes
                users = likes.Select(like => like.LikedUser);
            }

            //Gets the users that the currently logged-in user is LIKED BY
            if (likesParams.predicate == "likedBy")
            {
                //Does not run yet - simply adds on to the queries
                //Will look for all likes where the current user is the LikedUser
                likes = likes.Where(like => like.LikedUserId == likesParams.UserId);
                //Filters out the Users query to only include Users that the current user is liked by
                users = likes.Select(like => like.SourceUser);
            }

            var likedUsers = users.Select(user => new LikeDTO
            {
                UserName = user.UserName,
                KnownAs = user.KnownAs,
                Age = user.DateOfBirth.CalculateAge(),
                PhotoURL = user.Photos.FirstOrDefault(p => p.IsMain).Url,
                City = user.City,
                Id = user.Id
            });

            return await PagedList<LikeDTO>.CreateAsync(likedUsers, likesParams.PageNumber, likesParams.PageSize);
        }

        public async Task<AppUser> GetUserWithLikes(int userId)
        {
            return await _context.Users
                .Include(x => x.LikedUsers)
                .FirstOrDefaultAsync(x => x.Id == userId);
        }
    }
}