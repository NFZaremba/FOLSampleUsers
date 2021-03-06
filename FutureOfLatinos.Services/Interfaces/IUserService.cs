using FutureOfLatinos.Models;
using FutureOfLatinos.Models.Domain;
using FutureOfLatinos.Models.Requests;
using FutureOfLatinos.Models.ViewModels;
using System;
using System.Collections.Generic;

namespace FutureOfLatinos.Services
{
    public interface IUserService
    {
        int Create(RegistrationAddRequest userModel);
        IUserAuthData LogIn(string email, string password);
        List<int> GetPerson(int userID);
        bool LogInTest(string email, string password, int id, string[] roles = null);
        int CreateAuthToken(AuthTokenAddRequest model);
        AuthTokenViewModel GetByAuthTokenID(string ConfirmationAuthToken);
        EmailViewModel GetByEmail(string email);
        void UpdateIsConfirmed(EmailConfirmationUpdateRequest model);
        void Delete(int id);
        AuthTokenAddRequest AuthorizationToken(int id);
        EmailRequest GetEmail(Guid ConfirmationToken, string email);
        EmailRequest ForgotPasswordEmail(Guid ConfirmationToken, string email);
        void UpdatePassword(PasswordUpdateRequest model);
        IUserAuthData validatePassword(string email, string password);
        void Update(PasswordUpdateRequest model);
    }
}