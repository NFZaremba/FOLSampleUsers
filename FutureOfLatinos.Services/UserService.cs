using FutureOfLatinos.Data;
using FutureOfLatinos.Data.Providers;
using FutureOfLatinos.Models;
using FutureOfLatinos.Models.Domain;
using FutureOfLatinos.Models.Requests;
using FutureOfLatinos.Models.ViewModels;
using FutureOfLatinos.Services.Cryptography;
using FutureOfLatinos.Services.Interfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Security.Claims;
using System.Web.Hosting;

namespace FutureOfLatinos.Services
{
    public class UserService : BaseService, IUserService
    {
        private IAuthenticationService _authenticationService;
        private ICryptographyService _cryptographyService;
        private IDataProvider _dataProvider;
        private IConfigService _configService;
        private const int HASH_ITERATION_COUNT = 1;
        private const int RAND_LENGTH = 15;

        public UserService(IAuthenticationService authSerice, ICryptographyService cryptographyService, IDataProvider dataProvider, IConfigService configService)
        {
            _authenticationService = authSerice;
            _dataProvider = dataProvider;
            _cryptographyService = cryptographyService;
            _configService = configService;
        }

        public IUserAuthData LogIn(string email, string password)
        {
            string salt = GetSalt(email);
            if (!String.IsNullOrEmpty(salt))
            {
                string passwordHash = _cryptographyService.Hash(password, salt, HASH_ITERATION_COUNT);
                IUserAuthData response = Get(email, passwordHash);
                if (response != null && response.Name != null && response.Id != 0)
                {
                    _authenticationService.LogIn(response);
                    return response;
                }
            }
            return null;
        }

        public List<int> GetPerson(int UserId)
        {
            List<int> response = new List<int>();
            this.DataProvider.ExecuteCmd(
                "Get_Person_By_Id",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@Id", UserId);
                },
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    int index = 0;
                    response.Add(reader.GetInt32(index));
                }
            );
            return response;
        }

        public bool LogInTest(string email, string password, int id, string[] roles = null)
        {
            bool isSuccessful = false;
            IUserAuthData response = new UserBase
            {
                Id = id
                ,
                Name = "FakeUser" + id.ToString()
                ,
                Roles = roles ?? new[] { "User", "Super", "Content Manager" }
            };

            Claim tenant = new Claim("Tenant", "AAAA");
            Claim fullName = new Claim("FullName", "FutureOfLatinos Bootcamp");

            //Login Supports multiple claims
            //and multiple roles a good an quick example to set up for 1 to many relationship
            _authenticationService.LogIn(response, new Claim[] { tenant, fullName });

            return isSuccessful;
        }

        /// <summary>
        /// Gets the Data call to get a give user
        /// </summary>
        /// <param name="email"></param>
        /// <param name="passwordHash"></param>
        /// <returns></returns>
        private IUserAuthData Get(string email, string passwordHash)
        {
            UserBase response = new UserBase();
            List<string> role = new List<string>();
            this.DataProvider.ExecuteCmd(
                "Login_SelectPW",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@Email", email);
                    paramCol.AddWithValue("@Pass", passwordHash);
                },
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    switch (set)
                    {
                        case 0:
                            int index = 0;
                            response.Id = reader.GetInt32(index++);
                            response.Name = reader.GetString(index++);
                            break;
                        case 1:
                            role.Add(reader.GetString(0));
                            break;
                        default:
                            response = null;
                            break;
                    }
                }
            );
            response.Roles = role;
            return response;
        }

        private List<string> GetRoles(int id)
        {
            List<string> role = new List<string>();

            this.DataProvider.ExecuteCmd(
                "Users_Select_Role",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@Id", id);
                },
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    while (reader.Read())
                    {
                        role.Add(reader.GetString(0));
                    }
                }
            );
            return role;

        }

        /// <summary>
        /// The Dataprovider call to get the Salt for User with the given UserName/Email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        private string GetSalt(string email)
        {
            string model = null;
            this.DataProvider.ExecuteCmd(
                "Login_SelectId",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@Email", email);
                },
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    int index = 0;
                    model = reader.GetString(index++);
                }
            );
            return model;
        }

        // [CREATE]
        public int Create(RegistrationAddRequest userModel)
        {
            int result = 0;
            string salt;
            string passwordHash;
            string password = userModel.Password;
            bool isConfirmed = false;
            bool isActive = false;

            salt = _cryptographyService.GenerateRandomString(RAND_LENGTH);
            passwordHash = _cryptographyService.Hash(password, salt, HASH_ITERATION_COUNT);
            //DB provider call to create user and get us a user id
            this.DataProvider.ExecuteNonQuery(
                 "Users_Insert",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    SqlParameter parm = new SqlParameter();
                    parm.ParameterName = "@Id";
                    parm.SqlDbType = SqlDbType.Int;
                    parm.Direction = ParameterDirection.Output;
                    paramCol.Add(parm);
                    paramCol.AddWithValue("@FirstName", userModel.FirstName);
                    paramCol.AddWithValue("@LastName", userModel.LastName);
                    paramCol.AddWithValue("@Email", userModel.Email);
                    paramCol.AddWithValue("@Pass", passwordHash);
                    paramCol.AddWithValue("@Salt", salt);
                    paramCol.AddWithValue("@isConfirmed", isConfirmed);
                    paramCol.AddWithValue("@isActive", isActive);
                },
                    returnParameters: delegate (SqlParameterCollection paramCol)
                    {
                        result = (int)paramCol["@Id"].Value;
                    }
            );
            //be sure to store both salt and passwordHash
            //DO NOT STORE the original password value that the user passed us
            return result;
        }

        // [INSERT] to AuthToken datatable
        public int CreateAuthToken(AuthTokenAddRequest model)
        {
            int id = 0;
            this.DataProvider.ExecuteNonQuery(
                "AuthToken_Insert",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    SqlParameter parm = new SqlParameter();
                    parm.ParameterName = "@Id";
                    parm.SqlDbType = SqlDbType.Int;
                    parm.Direction = ParameterDirection.Output;
                    paramCol.Add(parm);

                    paramCol.AddWithValue("@UserID", model.UserId);
                    paramCol.AddWithValue("@ConfirmationToken", model.ConfirmationToken);
                }, returnParameters: delegate (SqlParameterCollection paramCol)
                {
                    id = (int)paramCol["@Id"].Value;
                }
            );
            return id;
        }

        // [SelectByAuthToken] from AuthToken datatable
        public AuthTokenViewModel GetByAuthTokenID(string ConfirmationAuthToken)
        {
            AuthTokenViewModel model = null; //set it to null because we want an empty set to return if the id doesn't exist
            this.DataProvider.ExecuteCmd(
                //map parameters
                "AuthToken_ById", //store procedure

                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@ConfirmationToken", ConfirmationAuthToken);
                },
                //once executed by store procedure we get results back (singleRecordMapper)
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    model = new AuthTokenViewModel();
                    int index = 0;
                    model.Id = reader.GetSafeInt32(index++);
                    model.UserId = reader.GetSafeInt32(index++);
                    model.Email = reader.GetSafeString(index++);
                    model.ConfirmationAuthToken = reader.GetSafeString(index++);
                    model.isConfirmed = reader.GetSafeBool(index++);
                    model.isActive = reader.GetSafeBool(index++);
                    model.CreatedDate = reader.GetDateTime(index++);
                }
            );
            return model;
        }

        public void UpdatePassword(PasswordUpdateRequest model)
        {
            string salt;
            string passwordHash;
            string password = model.Password;
            salt = _cryptographyService.GenerateRandomString(RAND_LENGTH);
            passwordHash = _cryptographyService.Hash(password, salt, HASH_ITERATION_COUNT);
            this.DataProvider.ExecuteNonQuery(
                 "Users_UpdatePassword",
                 inputParamMapper: delegate (SqlParameterCollection paramCol)
                 {
                     paramCol.AddWithValue("@Id", model.Id);
                     paramCol.AddWithValue("@Salt", salt);
                     paramCol.AddWithValue("@Pass", passwordHash);
                 }
            );
        }

        // [UpdateIsConfirmedUser]
        public void Update(EmailConfirmationUpdateRequest model)
        {
            this.DataProvider.ExecuteNonQuery(
                "Users_UpdateIsConfirmed", //store procedure
                inputParamMapper: delegate (SqlParameterCollection paramCol) //assign SqlParameterCollection a name called paramCol
                {
                    bool isConfirmed = true;
                    //send in all info from out model
                    //ModifiedDate and CreatedBy are handled by the store procedure
                    paramCol.AddWithValue("@Id", model.Id);
                    paramCol.AddWithValue("@isConfirmed", isConfirmed);
                }
            );
        }

        public EmailViewModel GetByEmail(string email)
        {
            EmailViewModel model = null; //set it to null because we want an empty set to return if the id doesn't exist
            this.DataProvider.ExecuteCmd(
                //map parameters
                "Users_SelectByEmail", //store procedure

                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@Email", email);
                },
                //once executed by store procedure we get results back (singleRecordMapper)
                singleRecordMapper: delegate (IDataReader reader, short set)
                {
                    model = new EmailViewModel();
                    int index = 0;
                    model.Id = reader.GetSafeInt32(index++);
                    model.Email = reader.GetSafeString(index++);
                    model.isConfirmed = reader.GetSafeBool(index++);
                }
            );
            return model;
        }

        // [UpdateIsConfirmedUser]
        public void UpdateIsConfirmed(EmailConfirmationUpdateRequest model)
        {
            this.DataProvider.ExecuteNonQuery(
                "Users_UpdateIsConfirmed", 
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    bool isConfirmed = true;
                    //send in all info from out model
                    //ModifiedDate and CreatedBy are handled by the store procedure
                    paramCol.AddWithValue("@Id", model.Id);
                    paramCol.AddWithValue("@isConfirmed", isConfirmed);
                }
            );
        }

        // [DELETE] AuthToken
        public void Delete(int id)
        {
            this.DataProvider.ExecuteNonQuery(
                "AuthToken_Delete",
                inputParamMapper: delegate (SqlParameterCollection paramCol)
                {
                    paramCol.AddWithValue("@UserId", id);
                }
            );
        }

        // email
        public EmailRequest GetEmail(Guid ConfirmationToken, string email)
        {
            EmailRequest emailRequest = new EmailRequest 
            {
                FromEmail = _configService.ConvertConfigValue_String("From_Email_Sabio"),
                FromName = _configService.ConvertConfigValue_String("From_Name"),
                ToEmail = email,
                Subject = _configService.ConvertConfigValue_String("Subject"),
                EmailTemplate = HostingEnvironment.MapPath("~/Content/ConfirmationEmailTemplate.html"),
                SendGridKey = _configService.ConvertConfigValue_String("SendGrid_Key"),
                ConfirmationLink = _configService.ConvertConfigValue_String("URL_email_template") + ConfirmationToken,
            };
            return emailRequest;
        }

        public EmailRequest ForgotPasswordEmail(Guid ConfirmationToken, string email)
        {
            EmailRequest emailRequest = new EmailRequest  
            {
                FromEmail = _configService.ConvertConfigValue_String("From_Email"),
                FromName = _configService.ConvertConfigValue_String("From_Name"),
                ToEmail = email,
                Subject = _configService.ConvertConfigValue_String("Subject"),
                EmailTemplate = HostingEnvironment.MapPath("~/Content/ForgotPasswordEmailTemp.html"),
                SendGridKey = _configService.ConvertConfigValue_String("SendGrid_Key"),
                ConfirmationLink = _configService.ConvertConfigValue_String("URL_reset_email") + ConfirmationToken,
            };
            return emailRequest;
        }

        // authorization token
        public AuthTokenAddRequest AuthorizationToken(int id)
        {
            AuthTokenAddRequest emailConfirmationToken = new AuthTokenAddRequest
            {   // set data to store in new AuthTokenAddRequest instance
                UserId = id, // set UserId
                ConfirmationToken = Guid.NewGuid() // set Guid 
            };
            return emailConfirmationToken;
        }

        public IUserAuthData validatePassword(string email, string password)
        {
            string salt = GetSalt(email);
            if (!String.IsNullOrEmpty(salt))
            {
                string passwordHash = _cryptographyService.Hash(password, salt, HASH_ITERATION_COUNT);
                IUserAuthData response = Get(email, passwordHash);
                if (response != null && response.Name != null && response.Id != 0)
                {
                    return response;
                }
            }
            return null;
        }

        public void Update(PasswordUpdateRequest model)
        {
            int Id = model.Id;
            string salt;
            string passwordHash;
            string password = model.Password;

            salt = _cryptographyService.GenerateRandomString(RAND_LENGTH);
            passwordHash = _cryptographyService.Hash(password, salt, HASH_ITERATION_COUNT);
            this.DataProvider.ExecuteNonQuery(
            "Users_UpdatePassword",
            inputParamMapper: delegate (SqlParameterCollection paramCol)
            {
                paramCol.AddWithValue("@Id", model.Id);
                paramCol.AddWithValue("@Pass", passwordHash);
                paramCol.AddWithValue("@Salt", salt);
            },
            returnParameters: null
               );
        }
    }
}