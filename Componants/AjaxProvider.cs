using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Compilation;
using System.Xml;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Localization;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuy.Components.Products;
using Nevoweb.DNN.NBrightBuy.Components.Interfaces;
using RazorEngine.Compilation.ImpromptuInterface.InvokeExt;
using DotNetNuke.Entities.Users;
using NBrightCore.render;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OpenStore.Providers.OS_Reports
{
    public class AjaxProvider : AjaxInterface
    {
        public override string Ajaxkey { get; set; }

        public override string ProcessCommand(string paramCmd, HttpContext context, string editlang = "")
        {
            if (!CheckRights())
            {
                return "Security Error.";
            }

            var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);
            var lang = NBrightBuyUtils.SetContextLangauge(ajaxInfo); // Ajax breaks context with DNN, so reset the context language to match the client.
            var objCtrl = new NBrightBuyController();

            var strOut = "OS_Reports Ajax Error";

            // NOTE: The paramCmd MUST start with the plugin ref. in lowercase. (links ajax provider to cmd)
            switch (paramCmd)
            {
                case "os_reports_test":
                    strOut = "<root>" + UserController.Instance.GetCurrentUserInfo().Username + "</root>";
                    break;
                case "os_reports_getdata":
                    strOut = GetData(context);
                    break;
                case "os_reports_addnew":
                    if (UserController.Instance.GetCurrentUserInfo().IsSuperUser)
                    {
                        strOut = GetData(context, true);
                    }
                    break;
                case "os_reports_deleterecord":
                    if (UserController.Instance.GetCurrentUserInfo().IsSuperUser)
                    {
                        strOut = DeleteData(context);
                    }
                    break;
                case "os_reports_savedata":
                    if (UserController.Instance.GetCurrentUserInfo().IsSuperUser)
                    {
                        strOut = SaveData(context);
                    }
                    break;
                case "os_reports_selectlang":
                    if (UserController.Instance.GetCurrentUserInfo().IsSuperUser)
                    {
                        strOut = SaveData(context);
                    }
                    break;
                case "os_reports_rundisplay":
                    strOut = GetData(context);
                    break;
                case "os_reports_runreport":
                    strOut = RunReport(context);
                    break;
            }

            return strOut;

        }

        public override void Validate()
        {
        }

        #region "Methods"

        private String GetData(HttpContext context, bool clearCache = false)
        {

            var objCtrl = new NBrightBuyController();
            var strOut = "";
            //get uploaded params
            var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);

            var itemid = ajaxInfo.GetXmlProperty("genxml/hidden/itemid");
            var typeCode = ajaxInfo.GetXmlProperty("genxml/hidden/typecode");
            var newitem = ajaxInfo.GetXmlProperty("genxml/hidden/newitem");
            var selecteditemid = ajaxInfo.GetXmlProperty("genxml/hidden/selecteditemid");
            var moduleid = ajaxInfo.GetXmlProperty("genxml/hidden/moduleid");
            var editlang = ajaxInfo.GetXmlProperty("genxml/hidden/editlang");
            if (editlang == "") editlang = Utils.GetCurrentCulture();

            var rundisplay = ajaxInfo.GetXmlPropertyBool("genxml/hidden/rundisplay");

            if (!Utils.IsNumeric(moduleid)) moduleid = "-2"; // use moduleid -2 for razor

            if (clearCache) NBrightBuyUtils.RemoveModCache(Convert.ToInt32(moduleid));

            if (newitem == "new") selecteditemid = AddNew(moduleid, typeCode);

            var templateControl = "/DesktopModules/NBright/OS_Reports";

            if (Utils.IsNumeric(selecteditemid))
            {
                // do edit field data if a itemid has been selected
                var obj = objCtrl.Get(Convert.ToInt32(selecteditemid), "", editlang);
                if (rundisplay)
                {
                    strOut = NBrightBuyUtils.RazorTemplRender("run.cshtml", Convert.ToInt32(moduleid), itemid + editlang + selecteditemid, obj, templateControl, "config", editlang, StoreSettings.Current.Settings());
                }
                else
                {
                    strOut = NBrightBuyUtils.RazorTemplRender("datafields.cshtml", Convert.ToInt32(moduleid), itemid + editlang + selecteditemid, obj, templateControl, "config", editlang, StoreSettings.Current.Settings());
                }
            }
            else
            {

                var pagenumber = ajaxInfo.GetXmlPropertyInt("genxml/hidden/pagenumber");
                var pagesize = ajaxInfo.GetXmlPropertyInt("genxml/hidden/pagesize");

                if (pagenumber == 0) pagenumber = 1;
                if (pagesize == 0) pagesize = 20;

                var filter = "";
                var searchText = ajaxInfo.GetXmlProperty("genxml/hidden/searchtext");
                if (searchText != "")
                {
                    filter += " and ( ";
                    filter += " (([xmldata].value('(genxml/textbox/ref)[1]', 'nvarchar(max)') like '%" + searchText + "%' collate sql_latin1_general_cp1_ci_ai ))";
                    filter += " or (([xmldata].value('(genxml/lang/genxml/textbox/ref2)[1]', 'nvarchar(max)') like '%" + searchText + "%' collate sql_latin1_general_cp1_ci_ai ))";
                    filter += " ) ";
                }

                // get only entity type required
                var recordcount = objCtrl.GetListCount(PortalSettings.Current.PortalId, -1, typeCode, filter);


                // Return list of items
                var l = objCtrl.GetDataList(PortalSettings.Current.PortalId, Convert.ToInt32(moduleid), typeCode, typeCode + "LANG", editlang, filter, " order by [XMLData].value('(genxml/textbox/ref)[1]','nvarchar(50)')", true,"", 0, pagenumber, pagesize, recordcount);
                strOut = NBrightBuyUtils.RazorTemplRenderList("datalist.cshtml", Convert.ToInt32(moduleid), "", l, templateControl, "config", editlang, StoreSettings.Current.Settings());

                if (recordcount > pagesize)
                {
                    var pg = new NBrightCore.controls.PagingCtrl();
                    strOut += pg.RenderPager(recordcount, pagesize, pagenumber);
                }

            }

            return strOut;

        }

        private String AddNew(String moduleid, String typeCode)
        {
            if (!Utils.IsNumeric(moduleid)) moduleid = "-2"; // -2 for razor

            var objCtrl = new NBrightBuyController();
            var nbi = new NBrightInfo(true);
            nbi.PortalId = PortalSettings.Current.PortalId;
            nbi.TypeCode = typeCode;
            nbi.ModuleId = Convert.ToInt32(moduleid);
            nbi.ItemID = -1;
            var itemId = objCtrl.Update(nbi);
            nbi.ItemID = itemId;

            foreach (var lang in DnnUtils.GetCultureCodeList(PortalSettings.Current.PortalId))
            {
                var nbi2 = new NBrightInfo(true);
                nbi2.PortalId = PortalSettings.Current.PortalId;
                nbi2.TypeCode = typeCode + "LANG";
                nbi2.ModuleId = Convert.ToInt32(moduleid);
                nbi2.ItemID = -1;
                nbi2.Lang = lang;
                nbi2.ParentItemId = itemId;
                nbi2.GUIDKey = "";
                nbi2.ItemID = objCtrl.Update(nbi2);
            }

            NBrightBuyUtils.RemoveModCache(nbi.ModuleId);

            return nbi.ItemID.ToString("");
        }

        private String SaveData(HttpContext context)
        {
            var objCtrl = new NBrightBuyController();

            //get uploaded params
            var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);

            var itemid = ajaxInfo.GetXmlProperty("genxml/hidden/itemid");
            var editlang = ajaxInfo.GetXmlProperty("genxml/hidden/editlang");
            if (editlang == "") editlang = Utils.GetCurrentCulture();

            if (Utils.IsNumeric(itemid))
            {
                // get DB record
                var nbi = objCtrl.Get(Convert.ToInt32(itemid));
                if (nbi != null)
                {
                    var typecode = nbi.TypeCode;

                    // get data passed back by ajax
                    var strIn = HttpUtility.UrlDecode(Utils.RequestParam(context, "inputxml"));
                    // update record with ajax data
                    nbi.UpdateAjax(strIn);
                    nbi.GUIDKey = nbi.GetXmlProperty("genxml/textbox/ref");
                    objCtrl.Update(nbi);

                    // do langauge record
                    var nbi2 = objCtrl.GetDataLang(Convert.ToInt32(itemid), editlang);
                    nbi2.UpdateAjax(strIn);
                    objCtrl.Update(nbi2);

                    DataCache.ClearCache(); // clear ALL cache.

                }
            }
            return "";
        }

        private String DeleteData(HttpContext context)
        {
            var objCtrl = new NBrightBuyController();

            //get uploaded params
            var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);
            var itemid = ajaxInfo.GetXmlProperty("genxml/hidden/itemid");
            if (Utils.IsNumeric(itemid))
            {
                // delete DB record
                objCtrl.Delete(Convert.ToInt32(itemid));

                NBrightBuyUtils.RemoveModCache(-2);
            }
            return "";
        }

        #endregion

        #region "Reports"

        private string RunReport(HttpContext context)
        {
            var strOut = "- No Data Returned -";
            var strSql = "";
            try
            {
                var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);
                //var itemid = ajaxInfo.GetXmlPropertyInt("genxml/hidden/itemid");
                var razortemplate = ajaxInfo.GetXmlProperty("genxml/hidden/razortemplate");
                var portalid = PortalSettings.Current.PortalId.ToString("");

                var editlang = ajaxInfo.GetXmlProperty("genxml/hidden/editlang");
                if (editlang == "") editlang = Utils.GetCurrentCulture();
                var selecteditemid = ajaxInfo.GetXmlPropertyInt("genxml/hidden/selecteditemid");

                if (selecteditemid > 0)
                {

                    var objCtrl = new NBrightBuyController();
                    var obj = objCtrl.Get(selecteditemid);
                    if (obj != null)
                    {
                        var inline = obj.GetXmlPropertyBool("genxml/checkbox/inline");
                        strSql = obj.GetXmlProperty("genxml/textbox/sql");
                        // replace any settings tokens (This is used to place the form data into the SQL)
                        strSql = Utils.ReplaceSettingTokens(strSql, ajaxInfo.ToDictionary());
                        strSql = Utils.ReplaceUrlTokens((strSql));

                        strSql = GenXmlFunctions.StripSqlCommands(strSql); // don't allow anything to update through here.

                        // add FOR XML if not there, this function will only output XML results.
                        if (!strSql.ToLower().Contains("for xml")) strSql += " FOR XML PATH ('item'), ROOT ('root')";

                        var strXmlResults = objCtrl.GetSqlxml(strSql);
                        if (strXmlResults != "")
                        {

                            var xdoc = XDocument.Parse(strXmlResults);
                            var headerInfo = new NBrightInfo(false);
                            headerInfo.XMLData = "<item></item>";
                            var elementsList = xdoc.XPathSelectElements("root/item[1]/*");
                            var xmlList = new List<NBrightInfo>();

                            if (obj.GetXmlProperty("genxml/radiobuttonlist/reportformat") == "csv")
                            {
                                var lp = 1;
                                foreach (XElement xmlitem in elementsList)
                                {
                                    headerInfo.SetXmlProperty("item/header" + lp, xmlitem.Name.ToString());
                                    lp += 1;
                                }
                                xmlList.Add(headerInfo);
                            }

                            foreach (XElement xmlitem in xdoc.XPathSelectElements("root/item"))
                            {
                                var nbi = new NBrightInfo(false);
                                nbi.XMLData = xmlitem.ToString();
                                xmlList.Add(nbi);
                            }

                            var templateControl = "/DesktopModules/NBright/OS_Reports";

                            if (obj.GetXmlProperty("genxml/radiobuttonlist/reportformat") == "html")
                            {
                                if (inline)
                                {
                                    razortemplate = "reporthtmlinline.cshtml";
                                }
                                else
                                {
                                    razortemplate = "reporthtml.cshtml";
                                }
                            }
                            if (obj.GetXmlProperty("genxml/radiobuttonlist/reportformat") == "csv")
                            {
                                inline = false;
                                razortemplate = "reportcsv.cshtml";
                            }
                            //project
                            if (obj.GetXmlProperty("genxml/radiobuttonlist/reportformat") == "barchart")
                            {
                                inline = true;
                                razortemplate = "reportbar.cshtml";
                            }

                            strOut = NBrightBuyUtils.RazorTemplRenderList(razortemplate, -1, "", xmlList, templateControl, "config",  editlang, StoreSettings.Current.Settings());

                            if (!inline)
                            {
                                var outfile = StoreSettings.Current.FolderTempMapPath + "\\" + selecteditemid + "." + obj.GetXmlProperty("genxml/radiobuttonlist/reportformat");
                                Utils.SaveFile(outfile, strOut);
                                strOut = "<br/><h1>Completed: <a target='_blank' href='" + StoreSettings.Current.FolderTemp + "/" + selecteditemid + "."
                                + obj.GetXmlProperty("genxml/radiobuttonlist/reportformat") + "?key=" + Utils.GetUniqueKey() + "' >View</a></h1>";
                            }

                        }
                        return strOut;
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.ToString() + " <hr/> " + strSql;
            }
            return strOut;
        }

        #endregion



        private Boolean CheckRights()
        {
            if (UserController.Instance.GetCurrentUserInfo().IsInRole(StoreSettings.ManagerRole) || UserController.Instance.GetCurrentUserInfo().IsInRole(StoreSettings.EditorRole) || UserController.Instance.GetCurrentUserInfo().IsInRole("Administrators"))
            {
                return true;
            }
            return false;
        }


    }
}
