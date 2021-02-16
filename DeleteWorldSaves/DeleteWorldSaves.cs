using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CoOpSpRpG;
using System.IO;
using Microsoft.Xna.Framework;
using WTFModLoader;
using WTFModLoader.Manager;
using System.Data.SQLite;
using System.Data;
using System.Reflection;

namespace DeleteWorldSaves
{
    public class PostfixPatches : IWTFMod
    {

        public ModLoadPriority Priority => ModLoadPriority.Normal;

        public void Initialize()
        {
            Harmony harmony = new Harmony("blacktea.DeleteWorldSaves");
            harmony.PatchAll();
        }

    }

    public class MyRootMenuRev2
    {

        public static SaveEntry focusedSave;
        public static ScrollCanvas MyscrollLoadCanvas;
        public static bool popupActive = false;
        public static GuiElement popupConfirmDelete;
        public static void deleteSave(GuiElement inp)
        {

            if (MyRootMenuRev2.focusedSave != null)
            {
                popupConfirmDelete.isVisible = true;
                popupActive = true;

            }

        }

        public static void MyselectEntry(SaveEntry entry)
        {
            // since we can not access any instance variables of the RootMenuRev2 without running a harmony patch, we replace RootMenuRev2.focusedSave with our own static varible which will have the same purpose as the original.
            // It holds the current save entry which is selected from the scroll canvas. Because we have to use our own variable we also have to repalce all methods of RootMenuRev2 which use it by our own. 
            // The problem possibly could be solved with reflections, however i am not sure how to do it.
            if(!DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.isVisible)
            {
            MyRootMenuRev2.focusedSave = entry;
            }

        }

        public static void loadGame(GuiElement inp)
        {
            // this method has to be called instead of RootMenuRev2.loadGame since we can not patch with Harmony the private original which uses instance variable. 
            if (MyRootMenuRev2.focusedSave != null)
            {
                string name = MyRootMenuRev2.focusedSave.name;
                DeleteWorldSaves.MyRootMenuRev2.databaseLoadSpecified(name); // we also have to make our own method for loading the save as the original can not be accessed without harmony patch/reflections
            }

        }

        public static void actionCancelDelete(object sender)
        {
            popupActive = false;
            popupConfirmDelete.isVisible = false;
        }

        public static void actionConfirmDelete(object sender)
        {
            if (popupActive)
            {
                // the next part of the code will try to delete the given save entry first in new format, if the file is not found, then in the old format.
                string path = Directory.GetCurrentDirectory() + "\\worldsaves\\" + MyRootMenuRev2.focusedSave.name + ".wdb"; // 0.9 format
                if (System.IO.File.Exists(path))
                {
                    File.Delete(path);
                    deleteContinueFileName(); //delete file from the characters "continue" database.
                }
                string legacypath = Directory.GetCurrentDirectory() + "\\worldsaves\\" + MyRootMenuRev2.focusedSave.name + ".wsav"; // pre 0.9 format
                if (System.IO.File.Exists(legacypath))
                {
                    File.Delete(legacypath);
                }

                // added for compatibility with my future mods. It will search for any possible mod save database associated with current worldsave and delete it.
                string[] modfiles = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\worldsaves\\", MyRootMenuRev2.focusedSave.name + "*", SearchOption.AllDirectories);
                for (int i = 0; i < modfiles.Count(); i++)
                {
                    var file = modfiles[i];
                    if (file.Contains("_MODSAVE"))
                        File.Delete(file);
                }

                // we have to execute the code from RootMenuRev2.actionOpenLoad again to update the savegame list after the file is deleted
                MyRootMenuRev2.MyscrollLoadCanvas.elementList.Clear();
                bool flag = !Directory.Exists(Directory.GetCurrentDirectory() + "\\worldsaves\\");
                if (flag)
                {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\worldsaves\\");
                }
                DirectoryInfo directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory() + "\\worldsaves\\");
                var extensions = new[] { "*.wdb", "*.wsav" };
                var files = extensions.SelectMany(ext => directoryInfo.GetFiles(ext));
                foreach (FileInfo fileInfo in files)
                {
                    string date = fileInfo.LastWriteTime.ToString("f", CONFIG.culture_en_US);
                    MyRootMenuRev2.MyscrollLoadCanvas.AddSaveEntry(Path.GetFileNameWithoutExtension(fileInfo.Name), date, SCREEN_MANAGER.white, 0, 4, 436, 52, SortType.vertical, new SaveEntry.ClickJournalEvent(DeleteWorldSaves.MyRootMenuRev2.MyselectEntry), null);
                }
            }
            popupActive = false;
            popupConfirmDelete.isVisible = false;
        }

        public static void deleteContinueFileName()
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            SQLiteConnection dBCon = typeof(CHARACTER_DATA).GetField("dBCon", flags).GetValue(null) as SQLiteConnection; //using reflections to access the static field dBCon from the class CHARACTER_DATA
            /* testing continues value replacement
            string commandText = "insert or replace into continues (name, game) select @name, @game where exists(select * from continues where game = '" + MyRootMenuRev2.focusedSave.name + "')";
            SQLiteCommand sqliteCommand = new SQLiteCommand(commandText, dBCon);
            sqliteCommand.Parameters.Add("@name", DbType.String, 32).Value = CHARACTER_DATA.selected;
            sqliteCommand.Parameters.AddWithValue("@game", DBNull.Value);
            sqliteCommand.ExecuteNonQuery();
            */
            string commandText = "delete from continues where game = '" + MyRootMenuRev2.focusedSave.name + "'";
            SQLiteCommand sqliteCommand = new SQLiteCommand(commandText, dBCon);
            sqliteCommand.ExecuteNonQuery();
        }

    public static void newVersion(string filename)
        {
            //a copy of the original code from 0.9 version of the game
            LoadingWorldScreen loadingWorldScreen = SCREEN_MANAGER.get_screen("loadWorld") as LoadingWorldScreen;
            loadingWorldScreen.mode = LoadScreenType.database_continue;
            CHARACTER_DATA.continueFileName = loadingWorldScreen.loadSelectName;
            loadingWorldScreen.loadSelectName = filename;
            loadingWorldScreen.loadSelectSet = true;
            SCREEN_MANAGER.goto_screen("loadWorld");

        }

        public static void oldVersion(string filename)
        {
            //a copy of the original code from 0.8 version of the game
            LoadingWorldScreen loadingWorldScreen = SCREEN_MANAGER.get_screen("loadWorld") as LoadingWorldScreen;
            loadingWorldScreen.mode = LoadScreenType.nuke_load_game;
            loadingWorldScreen.loadSelectName = filename;
            loadingWorldScreen.loadSelectSet = true;
            SCREEN_MANAGER.goto_screen("loadWorld");

        }

        public static void databaseLoadSpecified(string filename)
        {
            // this is where we check the version of the game to ensure that we use the correct worldsave format
            float versioncheck = Convert.ToSingle(CONFIG.version.Substring(0, 3), System.Globalization.CultureInfo.InvariantCulture);
            float comparevalue = Convert.ToSingle("0.9", System.Globalization.CultureInfo.InvariantCulture);
            if (versioncheck >= comparevalue)
            {
                DeleteWorldSaves.MyRootMenuRev2.newVersion(filename); // we have to split the actual loading code into separate methods as the field "CHARACTER_DATA.continueFileName" does not exist in the old version and will throw an exception once the method containing it is called by the old version of the game.
            }
            else
            {
                DeleteWorldSaves.MyRootMenuRev2.oldVersion(filename);
            }
        }



    }


    [HarmonyPatch(typeof(RootMenuRev2), "createElements")]
    public class RootMenuRev2_createElements

    {

        [HarmonyPostfix]
        // we use a postfix patch on RootMenuRev2.createElements to find and resize all parts of the "open game" dialogue to fit the new delete button, after that we clear out the original button container and replace them with our own buttons.
        private static void Postfix(RootMenuRev2 __instance, ref GuiElement ___popupLoad, ref ScrollCanvas ___scrollLoadCanvas, ref List<GuiElement> ___popupCanvas)
        {
            int num3 = 300;
            Color fontColor = new Color(226, 252, 255, 210);
            ___popupLoad.width = 450;
            bool openMP2 = CONFIG.openMP; // a check if we have the multiplayer version of the game is probably redundant at this point, but it will do no harm.
            if (!openMP2)
            {
                foreach (var entry in ___popupLoad.elementList)
                {
                    if (entry.name == "load scroll")
                    {
                        entry.width = 450;
                        foreach (var entry2 in ___scrollLoadCanvas.elementList)
                        {
                            entry2.width = 436;
                        }
                    }
                    else
                    {

                        entry.elementList.Clear();
                        entry.width = 450;
                        entry.AddButton("Load", SCREEN_MANAGER.white, 2, 2, num3 / 2 - 2 - 1, 36, new BasicButton.ClickEvent(DeleteWorldSaves.MyRootMenuRev2.loadGame), SCREEN_MANAGER.FF16, fontColor); // calling "DeleteWorldSaves.MyRootMenuRev2.loadGame" instead of RootMenuRev2.loadGame, private delegated methods have to be replaced by our own, not sure if it is posible to call and modify them with reflections, at this point this is the only solution a could think of.
                        entry.AddButton("Close", SCREEN_MANAGER.white, 2, 2, num3 / 2 - 2 - 1, 36, new BasicButton.ClickEvent(__instance.actionCloseLoad), SCREEN_MANAGER.FF16, fontColor); // we can use the original RootMenuRev2.actionCloseLoad method bacause it is public and can be accessed by harmony via instance of "RootMenuRev2"
                        entry.AddButton("Delete", SCREEN_MANAGER.white, 2, 2, num3 / 2 - 2 - 1, 36, new BasicButton.ClickEvent(DeleteWorldSaves.MyRootMenuRev2.deleteSave), SCREEN_MANAGER.FF16, CONFIG.textColorRed); // this is our new delete button with red text

                    }
                }
                // adding a confirmation dialogue
                ___popupCanvas.Add(new Canvas("confirm delete worldsave", SCREEN_MANAGER.white, 600, 600, 0, 0, 300, 80, SortType.vertical, new Color(14, 18, 19, 245)));
                DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete = ___popupCanvas.Last<GuiElement>();
                DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.addLabel("Confirm delete", SCREEN_MANAGER.FF20, 70, 0, 150, 40, CONFIG.textBrightColor);
                DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.AddCanvas("confirm delete worldsave buttons", SCREEN_MANAGER.white, 0, 0, 300, 40, SortType.horizontal);
                GuiElement guiElement6 = DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.elementList.Last<GuiElement>();
                guiElement6.AddButton("Confirm", SCREEN_MANAGER.white, 0, 0, 150, 40, new BasicButton.ClickEvent(DeleteWorldSaves.MyRootMenuRev2.actionConfirmDelete), SCREEN_MANAGER.FF16, CONFIG.textColorRed);
                guiElement6.AddButton("Cancel", SCREEN_MANAGER.white, 0, 0, 150, 40, new BasicButton.ClickEvent(DeleteWorldSaves.MyRootMenuRev2.actionCancelDelete), SCREEN_MANAGER.FF16, CONFIG.textBrightColor);
                DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.isVisible = false;
            }
        }

    }

    [HarmonyPatch(typeof(RootMenuRev2), "resize")]
    public class RootMenuRev2_resize

    {

        [HarmonyPostfix]
        private static void Postfix(ref int ___screenWidth, ref int ___screenHeight)
        {
            DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.reposition(___screenWidth / 2 - DeleteWorldSaves.MyRootMenuRev2.popupConfirmDelete.region.Height / 2, ___screenHeight / 2, false);
        }

    }




    // actionOpenLoad is called by clicking "Load world" button, it will be executed only once and only by this trigger. This is where any found savegames will be listed in scroll canvas.
    [HarmonyPatch(typeof(RootMenuRev2), "actionOpenLoad")]
    public class RootMenuRev2_actionOpenLoad
    {
        [HarmonyPrefix]
        private static Boolean Prefix( ref ScrollCanvas ___scrollLoadCanvas, ref GuiElement ___popupLoad, ref bool ___popupActive, ref SaveEntry ___focusedSave)
        {
            

            DeleteWorldSaves.MyRootMenuRev2.MyscrollLoadCanvas = ___scrollLoadCanvas;
            ___popupActive = true;
            ___scrollLoadCanvas.elementList.Clear();
            bool flag = !Directory.Exists(Directory.GetCurrentDirectory() + "\\worldsaves\\");
            if (flag)
            {
                Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\worldsaves\\");
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory() + "\\worldsaves\\");
            var extensions = new[] { "*.wdb", "*.wsav" }; // to make this patch compatible with 0.8 and 0.9 versions of the game we search for save files in the old and the new format
            var files = extensions.SelectMany(ext => directoryInfo.GetFiles(ext));
            foreach (FileInfo fileInfo in files)
            {
                string date = fileInfo.LastWriteTime.ToString("f", CONFIG.culture_en_US);
                // we have to replace RootMenuRev2.selectEntry method with our own, as we can not patch a private method with prefix or postfix patch. A reverse-patch would be an option if this method would not use any instance variables.
                // since it uses "this.focusedSave" "this." would be referring to an instance of our own class in a reverse-patch, which obviously would not work.
                MyRootMenuRev2.MyscrollLoadCanvas.AddSaveEntry(Path.GetFileNameWithoutExtension(fileInfo.Name), date, SCREEN_MANAGER.white, 0, 4, 436, 52, SortType.vertical, new SaveEntry.ClickJournalEvent(DeleteWorldSaves.MyRootMenuRev2.MyselectEntry), null);
            }
            ___popupLoad.isVisible = true;
            
            return false; // returning "false" in a prefix patch is an instruction for Harmony to supress execution of the original method, so only our replacement will be executed-

        }

    }

}
