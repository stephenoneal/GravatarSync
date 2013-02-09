using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading;
using System.ServiceProcess;




/* Other stuff TODO:
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * */

namespace GravatarSync
{
    public partial class Program : ServiceBase
    {
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                UpdateGravatar(true);
            }

            else
            {
                ServiceBase.Run(new Program());
            }
            
        }

        public Program()
        {
            this.ServiceName = "GravatarSync";
            System.Timers.Timer gravatarSyncTimer = new System.Timers.Timer();
            gravatarSyncTimer.Enabled = true;
            gravatarSyncTimer.Interval = 86400000;
            gravatarSyncTimer.Elapsed += new System.Timers.ElapsedEventHandler(gravatarSyncTimer_Elapsed);
        }

        void gravatarSyncTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            { UpdateGravatar(); }
            catch (Exception ex)
            {
            }

        }


        /// <summary>
        /// Call Gravatar Update Loop without the Console
        /// </summary>
        public static void UpdateGravatar()
        {
            UpdateGravatar(false);
        }

        /// <summary>
        /// Call Gravatar Update Loop and pass a boolean on whether to enable/disable console output.
        /// </summary>
        /// <param name="console">true = show console</param>
        public static void UpdateGravatar(bool console)
        {
            
            if (console) Console.WriteLine("Starting");
            
            IPGlobalProperties netproperties = IPGlobalProperties.GetIPGlobalProperties();
            
            
            
            DirectoryContext context = new DirectoryContext(DirectoryContextType.Domain,netproperties.DomainName);
            Domain d = Domain.GetDomain(context);
            DirectoryEntry de = d.GetDirectoryEntry();

            if (console) Console.WriteLine("domain is " + netproperties.DomainName);

            if (console) Console.WriteLine("LDAP is " + de.Properties["DistinguishedName"].Value.ToString());

            if (console) Console.ReadKey();

            DirectorySearcher ds = new DirectorySearcher("LDAP://" + de.Properties["DistinguishedName"].Value.ToString());

            ds.Filter = "(&(objectClass=user)(objectCategory=person))";


            //Attempt to append the LDAP search string specified in the app.config
            if (System.Configuration.ConfigurationManager.AppSettings["OUFilter"] != null)
            {
                if (System.Configuration.ConfigurationManager.AppSettings["OUFilter"].Length > 0)
                {
                    ds.Filter = "(&" + ds.Filter + System.Configuration.ConfigurationManager.AppSettings["OUFilter"] + ")";
                }
            }

            if (console) Console.WriteLine("Filter is" + ds.Filter);
            

            
            ds.PropertiesToLoad.Add("sAMAccountName");
            ds.PropertiesToLoad.Add("mail");
            ds.PageSize = 0;



            SearchResultCollection results = ds.FindAll();
            

            foreach (SearchResult result in results)
            {
                foreach (string PropertyName in result.Properties.PropertyNames)
                {

                    foreach (Object key in result.GetDirectoryEntry().Properties[PropertyName])
                    {

                        try
                        {
                            switch (PropertyName.ToUpper())
                            {


                                case "SAMACCOUNTNAME":
                                    if (console) Console.WriteLine(result.Path);
                                    if (console) Console.WriteLine(key.ToString());
                                    break;

                                case "MAIL":
                                    if (console) Console.WriteLine(key.ToString());
                                    using (MD5 md5Hash = MD5.Create())
                                    {
                                      
                                        String gravatarURI = "http://www.gravatar.com/avatar/" + GetMd5Hash(md5Hash,key.ToString()) + "?size=96&rating=G&d=" + System.Configuration.ConfigurationManager.AppSettings["DefaultGravatar"];
                                     
                                        WebRequest requestPNG = WebRequest.Create(gravatarURI);
                                        WebResponse responsePNG = requestPNG.GetResponse();
                                        Image webImage = Image.FromStream(responsePNG.GetResponseStream());
                                        MemoryStream ms = new MemoryStream();
                                        webImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                                        System.DirectoryServices.DirectoryEntry myUser = new System.DirectoryServices.DirectoryEntry(result.Path);
                                        myUser.Properties["thumbnailPhoto"].Clear();
                                        myUser.Properties["thumbnailPhoto"].Add(ms.ToArray());
                                        gravatarURI = "http://www.gravatar.com/avatar/" + GetMd5Hash(md5Hash, key.ToString()) + "?size=256&rating=G&d=" + System.Configuration.ConfigurationManager.AppSettings["DefaultGravatar"];
                                        requestPNG = WebRequest.Create(gravatarURI);
                                        responsePNG = requestPNG.GetResponse();
                                        webImage = Image.FromStream(responsePNG.GetResponseStream());
                                        ms = new MemoryStream();
                                        webImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                                        myUser.Properties["jpegPhoto"].Clear();
                                        myUser.Properties["jpegPhoto"].Add(ms.ToArray());
                                        myUser.CommitChanges();
                                        myUser.Close();
                                        myUser.Dispose();
                                        if (console) Console.WriteLine(GetMd5Hash(md5Hash, key.ToString()));
                                    }

                                    break;
                            }

                        }

                        catch { }


                    }
                }
            }
            de.Close();
            de.Dispose();
            results.Dispose();
            if (console) Console.WriteLine("Done Updating");
            //if (console) Console.ReadKey();
        }

        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }
    }
}
