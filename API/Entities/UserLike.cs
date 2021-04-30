namespace API.Entities
{
    public class UserLike
    {
        //SourceUser is the user initiating the Like - this relation will fill their LikedUser list with users they like
        public AppUser SourceUser { get; set; }
        public int SourceUserId { get; set; }

        //LikedUser is the user being liked - this relation will fill their LikedByUser list with users that they were liked by
        public AppUser LikedUser { get; set; }
        public int LikedUserId { get; set; }
    }
}