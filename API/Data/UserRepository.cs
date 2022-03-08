using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Dapper;

namespace API.Data
{
    public class UserRepository : IUserRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly string _connectionString;
        public UserRepository(DataContext context, IMapper mapper, IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _mapper = mapper;
            _context = context;
        }

        public async Task<MemberDTO> GetMemberAsync(string username)
        {
            return await _context.Users
                .Where(x => x.UserName.ToLower() == username.ToLower())
                .ProjectTo<MemberDTO>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
        }

		public async Task<PagedList<MemberDTO>> GetMembersAsync(UserParams userParams)
        {
            var query = _context.Users.AsQueryable();

            query = query.Where(u => u.UserName != userParams.CurrentUsername);
            query = query.Where(u => u.Gender == userParams.Gender);

            var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
            var maxDob = DateTime.Today.AddYears(-userParams.MinAge);

            query = query.Where(u => u.DateOfBirth >= minDob && u.DateOfBirth <= maxDob);

            query = userParams.OrderBy switch {
                "created" => query.OrderByDescending(u => u.Created),
                _ => query.OrderByDescending(u => u.LastActive)
            };

            return await PagedList<MemberDTO>.CreateAsync(query.ProjectTo<MemberDTO>(_mapper.ConfigurationProvider).AsNoTracking(), userParams.PageNumber, userParams.PageSize);

        }

        public async Task<AppUser> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<AppUser> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(p => p.Photos)
                .SingleOrDefaultAsync(x => x.UserName.ToLower() == username.ToLower());
        }

        public async Task<IEnumerable<AppUser>> GetUsersAsync()
        {
            return await _context.Users
                .Include(p => p.Photos)
                .ToListAsync();
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public void Update(AppUser user)
        {
            _context.Entry(user).State = EntityState.Modified;
        }

		
		public async Task<MemberDTO> GetMemberWithDapperAsync(string username)
		{
			using (var connection = new SqliteConnection(_connectionString)){
				var lookup = new Dictionary<int, MemberDTO>();
				var sql = @"select *
					from Users
					join Photos photos on photos.AppUserId = Users.Id
					where LOWER(UserName) LIKE LOWER(@UserName)";
				connection.Open();
				var user = await connection.QueryAsync<AppUser, PhotoDTO, MemberDTO>(
					sql, (user, photo) => {
						MemberDTO memberDto;
						if(!lookup.TryGetValue(user.Id, out memberDto)){
							memberDto = _mapper.Map(user, memberDto);
							lookup.Add(user.Id, memberDto);
						}
						if (memberDto.Photos == null) memberDto.Photos = new List<PhotoDTO>();
						memberDto.Photos.Add(photo);
						if (photo.IsMain) memberDto.PhotoURL = photo.URL;

						return memberDto;
					}, new { UserName = username });
				//var resultListMultiple = lookup.Values.ToList();
				var resultList = lookup.Values.FirstOrDefault();
				return resultList;
			}
		}

		public async Task<bool> UpdateMemberWithDapperAsync(MemberUpdateDTO memberUpdateDTO, string username)
		{
			using (var connection = new SqliteConnection(_connectionString)){
				var parameters = new DynamicParameters(memberUpdateDTO);
				parameters.Add("UserName", username);
				var sql = @"UPDATE Users
					SET LookingFor = @LookingFor,
					Introduction = @Introduction,
					Interests = @Interests,
					City = @City,
					Country = @Country
					WHERE LOWER(UserName) LIKE LOWER(@UserName)";
				connection.Open();
				var user = await connection.QueryAsync<MemberUpdateDTO>(sql, parameters);
				return user.FirstOrDefault() == null;
			}
		}

		public async Task<PagedList<MemberDTO>> GetMembersWithDapperAsync(UserParams userParams)
		{
			using (var connection = new SqliteConnection(_connectionString)){
				var orderBy = char.ToUpper(userParams.OrderBy[0]) + userParams.OrderBy.Substring(1);
				var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
				var maxDob = DateTime.Today.AddYears(-userParams.MinAge);
				var parameters = new DynamicParameters(new {
					CurrentUsername = userParams.CurrentUsername,
					Gender = userParams.Gender,
					MinDob = minDob,
					MaxDob = maxDob,
					Offset = (userParams.PageNumber - 1) * userParams.PageSize,
					PageSize = userParams.PageSize
				});
				var lookup = new Dictionary<int, MemberDTO>();
				var sql = @$"select *
					from Users
					join Photos photos on photos.AppUserId = Users.Id
					where UserName != @CurrentUsername
					and Gender = @Gender
					and DateOfBirth > @MinDob
					and DateOfBirth < @MaxDob
					ORDER BY {orderBy} DESC
					LIMIT @Offset, @PageSize";
				var sqlCount = @$"select count(Id) AS TotalCount
					from Users
					where UserName != @CurrentUsername
					and Gender = @Gender
					and DateOfBirth > @MinDob
					and DateOfBirth < @MaxDob";
				connection.Open();
				var user = await connection.QueryAsync<AppUser, PhotoDTO, MemberDTO>(
					sql, (user, photo) => {
						MemberDTO memberDto;
						if(!lookup.TryGetValue(user.Id, out memberDto)){
							memberDto = _mapper.Map(user, memberDto);
							lookup.Add(user.Id, memberDto);
						}
						if (memberDto.Photos == null) memberDto.Photos = new List<PhotoDTO>();
						memberDto.Photos.Add(photo);
						if (photo.IsMain) memberDto.PhotoURL = photo.URL;

						return memberDto;
					}, parameters);

				var count = await connection.ExecuteScalarAsync<int>(sqlCount, parameters);
				var resultList = lookup.Values.ToList();

				return new PagedList<MemberDTO>(resultList, count, userParams.PageNumber, userParams.PageSize);
			}
		}
	}
}