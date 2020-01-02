﻿/************************************************************************
 * Copyright (C) 2015 by themirage <mirage@secure-mail.biz>             *
 *                                                                      *
 * This program is free software: you can redistribute it and/or modify *
 * it under the terms of the GNU General Public License as published by *
 * the Free Software Foundation, either version 3 of the License, or    *
 * (at your option) any later version.                                  *
 *                                                                      *
 * This program is distributed in the hope that it will be useful,      *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of       *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the        *
 * GNU General Public License for more details.                         *
 *                                                                      *
 * You should have received a copy of the GNU General Public License    *
 * along with this program.  If not, see <http://www.gnu.org/licenses/> *
 ************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace GChan
{
    internal class EightKun : Imageboard
    {
        public const string regThread = "8kun.top/[a-zA-Z0-9]*?/res/[0-9]*.[^0-9]*";  // Regex to check whether is Thread or not
        public const string regBoard = "8kun.top/[a-zA-Z0-9]*?/";                     // Regex to check whether is Board or not

        public EightKun(string url, bool isBoard) : base(url, isBoard)
        {
            this.board = isBoard;
            this.siteName = "8kun";
            if (!isBoard)
            {
                Match match = Regex.Match(url, @"8kun.top.net/[a-zA-Z0-9]*?/res/[0-9]*");
                this.URL = "http://" + match.Groups[0].Value + ".html";      // simplify thread url
                this.SaveTo = (Properties.Settings.Default.path + "\\" + this.siteName + "\\" + getURL().Split('/')[3] + "\\" + getURL().Split('/')[5]).Replace(".html", ""); // set saveto path
            }
            else
            {
                this.URL = url;
                this.SaveTo = Properties.Settings.Default.path + "\\" + this.siteName + "\\" + getURL().Split('/')[3]; // set saveto path
            }
        }

        public new static bool urlIsThread(string url)
        {
            Regex urlMatcher = new Regex(regThread);
            if (urlMatcher.IsMatch(url))
                return true;
            else
                return false;
        }

        public new static bool urlIsBoard(string url)
        {
            Regex urlMatcher = new Regex(regBoard);
            if (urlMatcher.IsMatch(url))
                return true;
            else
                return false;
        }

        override protected ImageLink[] getLinks()
        {
            List<ImageLink> links = new List<ImageLink>();
            string JSONUrl = ("http://8kun.top/" + getURL().Split('/')[3] + "/res/" + getURL().Split('/')[5] + ".json").Replace(".html", ""); // thread JSON url
            string str = "";
            XmlNodeList xmlTim, xmlFilenames, xmlExt;

            try
            {
                string Content = new WebClient().DownloadString(JSONUrl);

                byte[] bytes = Encoding.ASCII.GetBytes(Content);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    var xml = XDocument.Load(jsonReader);
                    str = xml.ToString();                                                               // convert JSON to XML (funny, I know)
                }

                // get single images
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(str);
                xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/tim");
                xmlFilenames = doc.DocumentElement.SelectNodes("/root/posts/item/filename");
                xmlExt = doc.DocumentElement.SelectNodes("/root/posts/item/ext");

                for (int i = 0; i < xmlExt.Count; i++)
                {
                    //exed = exed + "https://8kun.top/" + getURL().Split('/')[3] + "/src/" + xmlTim[i].InnerText + xmlExt[i].InnerText + "\n";
                    links.Add(new ImageLink("https://8kun.top/" + "/file_store/" + xmlTim[i].InnerText + xmlExt[i].InnerText, xmlFilenames[i].InnerText));
                }

                // get images of posts with multiple images
                xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/extra_files/item/tim");
                xmlFilenames = doc.DocumentElement.SelectNodes("/root/posts/item/extra_files/item/filename");
                xmlExt = doc.DocumentElement.SelectNodes("/root/posts/item/extra_files/item/ext");

                for (int i = 0; i < xmlExt.Count; i++)
                {
                    //exed = exed + "https://8kun.top/" + getURL().Split('/')[3] + "/src/" + xmlTim[i].InnerText + xmlExt[i].InnerText + "\n";
                    links.Add(new ImageLink("https://8kun.top/" + "/file_store/" + xmlTim[i].InnerText + xmlExt[i].InnerText, xmlFilenames[i].InnerText));
                }
            }
            catch (WebException webEx)
            {
                if (((int)webEx.Status) == 7)                                               // 404
                    this.Gone = true;
                throw;
            }

            return links.ToArray();
        }

        override public string[] getThreads()
        {
            string URL = "http://8kun.top/" + getURL().Split('/')[3] + "/catalog.json";
            List<string> Res = new List<string>();
            string str = "";
            XmlNodeList tNo;
            try
            {
                string json = new WebClient().DownloadString(URL);
                byte[] bytes = Encoding.ASCII.GetBytes(json);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    var xml = XDocument.Load(jsonReader);
                    str = xml.ToString();                                                 // JSON to XML again
                }

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(str);
                tNo = doc.DocumentElement.SelectNodes("/root/item/threads/item/no");
                for (int i = 0; i < tNo.Count; i++)
                {
                    Res.Add("http://8kun.top/" + getURL().Split('/')[3] + "/res/" + tNo[i].InnerText + ".html");
                }
            }
            catch (WebException webEx)
            {
                Program.Log(webEx);
#if DEBUG
                MessageBox.Show("Connection Error: " + webEx.Message);
#endif
            }

            return Res.ToArray();
        }

        protected override string GetThreadSubject()
        {
            //TODO: Implement.
            throw new NotImplementedException();
        }

        override public void download()
        {
            try
            {
                if (!Directory.Exists(this.SaveTo))
                    Directory.CreateDirectory(this.SaveTo);

                if (Properties.Settings.Default.loadHTML)
                    downloadHTMLPage();

                ImageLink[] URLs = getLinks();

                for (int y = 0; y < URLs.Length; y++)
                    Utils.DownloadToDir(URLs[y], this.SaveTo);
            }
            catch (WebException webEx)
            {
                if (((int)webEx.Status) == 7)
                    this.Gone = true;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(ex.Message, "No Permission to access folder");
                throw;
            }
        }

        private void downloadHTMLPage()
        {
            List<string> thumbs = new List<string>();
            string htmlPage = "";
            string str;

            try
            {
                htmlPage = new WebClient().DownloadString(this.getURL());

                string JURL = this.getURL().Replace(".html", ".json");

                string Content = new WebClient().DownloadString(JURL);

                byte[] bytes = Encoding.ASCII.GetBytes(Content);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    var xml = XDocument.Load(jsonReader);
                    str = xml.ToString();
                }

                // get single images
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(str);
                XmlNodeList xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/tim");
                XmlNodeList xmlExt = doc.DocumentElement.SelectNodes("/root/posts/item/ext");
                for (int i = 0; i < xmlExt.Count; i++)
                {
                    string ext = xmlExt[i].InnerText;
                    //                        if(ext == ".webm")
                    //                            ext = ".jpg";
                    thumbs.Add("https://8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext);

                    htmlPage = htmlPage.Replace("https://8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext, "thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("=\"/file_store/thumb/" + xmlTim[i].InnerText + ext, "=\"thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("=\"/file_store/" + xmlTim[i].InnerText + ext, "=\"" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://media.8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext, "thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://media.8kun.top/file_store/" + xmlTim[i].InnerText + ext, xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://8kun.top/file_store/" + xmlTim[i].InnerText + ext, xmlTim[i].InnerText + ext);
                }

                // get images of posts with multiple images
                xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/extra_files/item/tim");
                xmlExt = doc.DocumentElement.SelectNodes("/root/posts/item/extra_files/item/ext");
                for (int i = 0; i < xmlExt.Count; i++)
                {
                    string ext = xmlExt[i].InnerText;
                    //                        if(ext == ".webm")
                    //                            ext = ".jpg";
                    thumbs.Add("https://8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext);

                    htmlPage = htmlPage.Replace("https://8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext, "thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("=\"/file_store/thumb/" + xmlTim[i].InnerText + ext, "=\"thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("=\"/file_store/" + xmlTim[i].InnerText + ext, "=\"" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://media.8kun.top/file_store/thumb/" + xmlTim[i].InnerText + ext, "thumb/" + xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://media.8kun.top/file_store/" + xmlTim[i].InnerText + ext, xmlTim[i].InnerText + ext);
                    htmlPage = htmlPage.Replace("https://8kun.top/file_store/" + xmlTim[i].InnerText + ext, xmlTim[i].InnerText + ext);
                }

                htmlPage = htmlPage.Replace("=\"/", "=\"https://8kun.top/");

                for (int i = 0; i < thumbs.Count; i++)
                {
                    Utils.DownloadToDir(thumbs[i], this.SaveTo + "\\thumb");
                }

                if (!String.IsNullOrWhiteSpace(htmlPage))
                    File.WriteAllText(this.SaveTo + "\\Thread.html", htmlPage); // save thread
            }
            catch
            {
                throw;
            }
        }

        public override void download(object callback)
        {
            Console.WriteLine("Downloading: " + URL);
            download();
        }
    }
}