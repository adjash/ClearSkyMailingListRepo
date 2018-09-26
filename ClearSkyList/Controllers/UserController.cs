using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using ClearSkyList.Models;
namespace ClearSkyList.Controllers
{
    public class UserController : Controller
    {
        //Registration Action

        [HttpGet]
        public ActionResult Registration()
        {
            return View();
        }

        //Registration POST action

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registration([Bind(Exclude = "EmailVerified,ActivationCode")]User user)
        {
            bool Status = false;
            string message = "";
            //
            //Model validaiton 
            if (ModelState.IsValid)
            {
                #region //Email Already Exists
                var isExist = isEmailExist(user.Email);
                if (isExist)
                {
                    ModelState.AddModelError("EmailExist", "Email Already Exists ");
                    return View(user);
                }
                #endregion

                #region //Generate Activation Code
                user.ActivationCode = Guid.NewGuid();
                #endregion

                #region //Password hashing
                user.Password = Crypto.Hash(user.Password);
                user.ConfirmPassword = Crypto.Hash(user.ConfirmPassword);//
                #endregion

                user.EmailVerified = false;

                #region //Saving to Database
                using (CsDBEntities cs = new CsDBEntities())
                {
                    cs.Users.Add(user);
                    cs.SaveChanges();

                    //Send Email Verification to user
                    SendVerificationLinkEmail(user.Email, user.ActivationCode.ToString());
                    message = "Registration Successful. Activation Link has been sent to:" +user.Email;
                    Status = true;
                }
                #endregion
            }
            else
            {
                message = "Invalid request";
            }

            ViewBag.Message = message;
            ViewBag.Status = Status;

            //Send Email to User
            return View(user);
        }
        //Verify Email
        
        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool Status = false;
            using (CsDBEntities cs = new CsDBEntities())
            {
                cs.Configuration.ValidateOnSaveEnabled = false;

                var v = cs.Users.Where(a => a.ActivationCode == new Guid(id)).FirstOrDefault();
                if (v != null)
                {
                    v.EmailVerified = true;
                    cs.SaveChanges();
                    Status = true;
                }
                else
                {
                    ViewBag.Message = "Invalid Request";
                }
            }
            ViewBag.Status = Status;
                return View();
        }

        //Login
        [HttpGet]
        public ActionResult Login()
        {

            return View();
        }


        //Login POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserLogin login, string ReturnUrl="")
        {
            string message = "";
            using (CsDBEntities cs = new CsDBEntities())
            {
                var v = cs.Users.Where(a => a.Email == login.Email).FirstOrDefault();
                if (v != null)
                {
                    if (string.Compare(Crypto.Hash(login.Password), v.Password) == 0)
                    {
                        int timeout = login.RememberMe ? 525600 : 1;
                        var ticket = new FormsAuthenticationTicket(login.Email, login.RememberMe, timeout);
                        string encrypted = FormsAuthentication.Encrypt(ticket);
                        var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                        cookie.Expires = DateTime.Now.AddMinutes(timeout);
                        cookie.HttpOnly = true;
                        Response.Cookies.Add(cookie);

                        if (Url.IsLocalUrl(ReturnUrl))
                        {
                            return Redirect(ReturnUrl);
                        }
                        else
                        {
                            return RedirectToAction("index", "Home");
                        }

                    }else
                    {
                        message = "Invalid Credentials";
                    }
                }else
                {
                    message = "Invalid Credentials";
                }
            }
                ViewBag.Message = message;
            return View();
        }


        //Logout
        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }

        [NonAction]
        public bool isEmailExist(String emailID)
        {
            using (CsDBEntities cs = new CsDBEntities())
            {
                var v = cs.Users.Where(a => a.Email == emailID).FirstOrDefault();
                return v != null;
            }
        }

        [NonAction]
        public void SendVerificationLinkEmail(string emailID, string activationCode)
        {
            var verifyUrl = "/User/VerifyAccount/"+activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery,verifyUrl);

            var fromEmail = new MailAddress("clearskytestemail1123@gmail.com", "ClearSky");
            var toEmail = new MailAddress(emailID);
            var fromEmailPassword = "UnsecurePass123";

            string subject = "Your account is successfully created!";

            string body = "<br/><br/> You have successfully set up your account for the clearsky mailing list."
                + "Please click on the link below to verify your account:" +
                "<br/><br/><a href='" +link+ "'></a>";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, fromEmailPassword)
            };

            using (var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = subject,
                Body = body + link,
                IsBodyHtml = true,
            })
                smtp.Send(message);
        }
    } 
}