using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarathonApp.DAL.Entities;
using MarathonApp.DAL.Models.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace MarathonApp.BLL.Services
{

    public interface IUserService
    {
        Task<UserManagerResponse> RegisterOwnerAsync();
        Task<UserManagerResponse> RegisterAdminAsync(AdminOwnerRegisterModel model);
        Task<UserManagerResponse> RegisterAsync(RegisterViewModel model);
        Task<UserManagerResponse> LoginAsync(LoginViewModel model);
        Task<UserManagerResponse> ConfirmEmailAsync(string userIs, string token);
        Task<UserManagerResponse> ForgetPasswordAsync(string email);
        Task<UserManagerResponse> ResetPasswordAsync(ResetPasswordViewModel model);
    }


    public class UserService : IUserService
    {
        private UserManager<User> _userManager;
        private IConfiguration _configuration;
        private IEmailService _emailService;
        private RoleManager<IdentityRole> _roleManager;

        public UserService(UserManager<User> userManager, IConfiguration configuration, IEmailService emailService, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _configuration = configuration;
            _emailService = emailService;
            _roleManager = roleManager;
        }

        public async Task<UserManagerResponse> RegisterOwnerAsync()
        {
            var owner = await _userManager.GetUsersInRoleAsync("Owner");
            if (owner.Count != 0)
                return new UserManagerResponse
                {
                    Message = "Owner is already exist",
                    IsSuccess = false
                };

            var identityUser = new User
            {
                Email = _configuration.GetSection("OwnerInfo:Username").Value,
                UserName = _configuration.GetSection("OwnerInfo:Username").Value
            };
            var password = _configuration.GetSection("OwnerInfo:Password").Value;

            var result = await _userManager.CreateAsync(identityUser, password);


            if (result.Succeeded)
                await _userManager.AddToRoleAsync(identityUser, UserRolesModel.Owner);
                return new UserManagerResponse
                {
                    Message = "Owner was successfully registrated",
                    IsSuccess = true
                };
            return new UserManagerResponse
            {
                Message = "Owner is not created",
                IsSuccess = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        public async Task<UserManagerResponse> RegisterAdminAsync(AdminOwnerRegisterModel model)
        {
            if (model == null)
                throw new NullReferenceException("Register form is empty");

            var identityUser = new User
            {
                Email = model.Email,
                UserName = model.Email
            };
            var password = model.Password;

            var result = await _userManager.CreateAsync(identityUser, password);


            if (result.Succeeded)
                await _userManager.AddToRoleAsync(identityUser, UserRolesModel.Admin);
            return new UserManagerResponse
            {
                Message = "Admin was successfully registrated",
                IsSuccess = true
            };
            return new UserManagerResponse
            {
                Message = "Admin is not created",
                IsSuccess = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        public async Task<UserManagerResponse> RegisterAsync(RegisterViewModel model)
        {
            if (model == null)
                throw new NullReferenceException("Register form is empty");

            if (model.Password != model.ConfirmPassword)
                return new UserManagerResponse
                {
                    Message = "Passwords do not match",
                    IsSuccess = false
                };

            var identityUser = new User
            {
                Email = model.Email,
                UserName = model.Email
            };

            var result = await _userManager.CreateAsync(identityUser, model.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync(UserRolesModel.Admin))
                    await _roleManager.CreateAsync(new IdentityRole(UserRolesModel.Admin));
                if (!await _roleManager.RoleExistsAsync(UserRolesModel.User))
                    await _roleManager.CreateAsync(new IdentityRole(UserRolesModel.User));
                if (!await _roleManager.RoleExistsAsync(UserRolesModel.Owner))
                    await _roleManager.CreateAsync(new IdentityRole(UserRolesModel.Owner));

                await _userManager.AddToRoleAsync(identityUser, UserRolesModel.User);

                await _emailService.SendConfirmEmailAsync(identityUser);

                return new UserManagerResponse
                {
                    Message = "User was successfully registrated",
                    IsSuccess = true
                };
            }
            return new UserManagerResponse
            {
                Message = "User is not created",
                IsSuccess = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        public async Task<UserManagerResponse> LoginAsync(LoginViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return new UserManagerResponse
                {
                    Message = "There is no user with that email address",
                    IsSuccess = false
                };

            var result = await _userManager.CheckPasswordAsync(user, model.Password);

            if (!result)
                return new UserManagerResponse
                {
                    Message = "Incorrect password",
                    IsSuccess = false
                };

            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, model.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("AuthSettings:Key").Value));

            var token = new JwtSecurityToken(
                issuer: "me",
                audience: "you",
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
                );

            string tokenAsString = new JwtSecurityTokenHandler().WriteToken(token);

            return new UserManagerResponse
            {
                Message = tokenAsString,
                IsSuccess = true,
                Expiration = token.ValidTo
            };
        }

        public async Task<UserManagerResponse> ConfirmEmailAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return new UserManagerResponse
                {
                    Message = "User not found",
                    IsSuccess = false,
                };
            var decodedToken = WebEncoders.Base64UrlDecode(token);

            string normalToken = Encoding.UTF8.GetString(decodedToken);

            var result = await _userManager.ConfirmEmailAsync(user, normalToken);

            if (result.Succeeded)
                return new UserManagerResponse
                {
                    Message = "Email confirmed successfully!",
                    IsSuccess = true
                };
            return new UserManagerResponse
            {
                Message = "Email was not confirmed",
                IsSuccess = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        public async Task<UserManagerResponse> ForgetPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
                return new UserManagerResponse
                {
                    Message = "There is no user with that email address",
                    IsSuccess = false
                };

            await _emailService.ForgetPasswordEmailAsync(user, email);

            return new UserManagerResponse
            {
                Message = "Password reset URL has been sent to the email successfully!",
                IsSuccess = true
            };
        }

        public async Task<UserManagerResponse> ResetPasswordAsync(ResetPasswordViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return new UserManagerResponse
                {
                    Message = "There is no user with that email address",
                    IsSuccess = false
                };

            if (model.NewPassword != model.ConfirmPassword)
                return new UserManagerResponse
                {
                    Message = "Password do not match",
                    IsSuccess = false
                };

            var decodedToken = WebEncoders.Base64UrlDecode(model.Token);

            string normalToken = Encoding.UTF8.GetString(decodedToken);

            var result = await _userManager.ResetPasswordAsync(user, normalToken, model.NewPassword);

            if (result.Succeeded)
                return new UserManagerResponse
                {
                    Message = "Password has been reset successfully!",
                    IsSuccess = true
                };
            return new UserManagerResponse
            {
                Message = "Something went wrong",
                IsSuccess = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }
    }
}

