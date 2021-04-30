using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;

namespace API.Interfaces
{
    public interface ILikesRepository
    {
        Task<UserLike> GetUserLike(int sourceUserId, int likedUserId);
        Task<AppUser> GetUserWithLikes(int userId);
        //This method will be for one specific user (userId). The "predicate" param will tell us if we're getting a list of all the users this current user LIKES, or a list of every user that this one has been LIKED BY. (It basically changes whether we're looking for the passed-in user as the SourceUser or the LikedUser)
        Task<PagedList<LikeDTO>> GetUserLikes(LikesParams likesParams);
    }
}