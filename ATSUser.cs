using System;
using ServiceStack.Auth;

namespace ATS.DarkSearch;

public class ATSUser : UserAuth
{
    public DateTime? LastLoginDate { get; set; }
}