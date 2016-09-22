﻿using System;
using System.IO;
using System.Linq;
using tud.mci.tangram.audio;

namespace tud.mci.tangram.TangramLector.SpecializedFunctionProxies
{
    /// <summary>
    /// Class for manipulating OpenOffice Draw document elements 
    /// </summary>
    public partial class OpenOfficeDrawShapeManipulator : AbstractSpecializedFunctionProxyBase
    {

        #region Modification Mode Carousel

        private TimeSpan doubleKlickTolerance = new TimeSpan(0, 0, 0, 0, 300);
        private DateTime lastRequest = DateTime.Now;

        private readonly int _maxMode = Enum.GetValues(typeof(ModificationMode)).Cast<int>().Max();

        private void rotateThroughModes()
        {
            if (LastSelectedShape == null)
            {
                return;
            }
            if (LastSelectedShapePolygonPoints != null)
            {
                Mode = ModificationMode.Move;
                playError();
                return;
            }

            DateTime timestamp = DateTime.Now;

            object val = Convert.ChangeType(Mode, Mode.GetTypeCode());
            int m = Convert.ToInt32(val);

            // mode switch
            // should not rotate to UNKOWN = 0
            if (timestamp - lastRequest > doubleKlickTolerance) {
                m = Math.Max(1, (++m) % (_maxMode + 1));
            }
            else { // double click ... rotate backwards
                m = (m - 2) % (_maxMode + 1);
                if (m <= 0) m = _maxMode + m;
            }

            lastRequest = timestamp;

            Mode = (ModificationMode)Enum.ToObject(typeof(ModificationMode), m);
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MODE SWITCH] to the new mode: " + Mode);
            comunicateModeSwitch(Mode);
        }

        private void comunicateModeSwitch(ModificationMode mode)
        {
            String audio = getAudioFeedback(mode);
            String detail = getDetailRegionFeedback(mode);

            AudioRenderer.Instance.PlaySoundImmediately(audio);
            sentTextFeedback(detail);
        }

        #endregion

        #region Manipulation

        private double getZoomLevel()
        {
            if (Zoomable != null)
            {
                return Zoomable.GetZoomLevel();
            }

            return 1;
        }

        private int getStep()
        {
            // get the right step - depending on the zoom level?!
            //
            // you have to move at least one visible pixel
            // visible pixel depends on the resolution of the output device
            // you need at least 10 dpi for displaying Braille
            // one inch is 25.4 mm --> one dot is circa 2.54 mm in zoom level 1.0
            // need over-sampling to see every change -->  3 mm change at least
            //
            // zoom factor is relation between one pixel and one dot
            // resolution of a screen should be circa 96 dpi
            // one pixel is circa 0.265 mm -->  3.78 p/mm
            // --> size in pixel / 3.78 = size in mm

            double zoom = getZoomLevel();
            //double change = (300 / zoom) / 3.78;
            double change = (150 / zoom) / 3.78;
            return (int)Math.Round(change, MidpointRounding.AwayFromZero);
        }

        private int getSizeSteps() { return getStep() * 2; }

        private int getLargeDegree() { return 1500; }

        private int getSmallDegree() { return 100; }

        private bool emptyShapeForManipulationError()
        {
            if (LastSelectedShape == null)
            {
                return true;
            }
            return false;
        }

        #region Mode Based Functions

        private bool handleUP()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " up");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    //Mode = ModificationMode.Move;
                    //moveShapeVertical(-getStep());
                    return false;
                case ModificationMode.Move:
                    moveShapeVertical(-getStep());
                    break;
                case ModificationMode.Scale:
                    scaleHeight(getSizeSteps());
                    break;
                case ModificationMode.Rotate:
                    rotateLeft(-getSmallDegree());
                    break;
                case ModificationMode.Fill:
                    changeFillStyle(-1);
                    return true;
                // FIXME: getDetailregionFeedback is not correct --> detail region content is set in changeFillStyle function
                case ModificationMode.Line:
                    changeLineWidth(50);
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }



        private bool handleDOWN()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " down");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    //Mode = ModificationMode.Move;
                    //moveShapeVertical(getStep());
                    return false;
                case ModificationMode.Move:
                    moveShapeVertical(getStep());
                    break;
                case ModificationMode.Scale:
                    scaleHeight(-getSizeSteps());
                    break;
                case ModificationMode.Rotate:
                    rotateLeft(getSmallDegree());
                    break;
                case ModificationMode.Fill:
                    changeFillStyle(1);
                    return true; // getDetailregionFeedback is not correct --> detail region content is set in changeFillStyle function
                case ModificationMode.Line:
                    changeLineWidth(-50);
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleLEFT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " left");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    //Mode = ModificationMode.Move;
                    //moveShapeHorizontal(-getStep());
                    return false;
                case ModificationMode.Move:
                    moveShapeHorizontal(-getStep());
                    break;
                case ModificationMode.Scale:
                    scaleWidth(-getSizeSteps());
                    break;
                case ModificationMode.Rotate:
                    rotateLeft(getLargeDegree());
                    break;
                case ModificationMode.Fill:
                    changeFillStyle(-1);
                    return true; // getDetailregionFeedback is not correct --> detail region content is set in changeFillStyle function
                case ModificationMode.Line:
                    changeLineStyle(-1);
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleRIGHT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " right");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    //Mode = ModificationMode.Move;
                    //moveShapeHorizontal(getStep());
                    return false;
                case ModificationMode.Move:
                    moveShapeHorizontal(getStep());
                    break;
                case ModificationMode.Scale:
                    scaleWidth(getSizeSteps());
                    break;
                case ModificationMode.Rotate:
                    rotateRight(getLargeDegree());
                    break;
                case ModificationMode.Fill:
                    changeFillStyle(1);
                    return true; // getDetailregionFeedback is not correct --> detail region content is set in changeFillStyle function
                case ModificationMode.Line:
                    changeLineStyle(1);
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleUP_RIGHT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " up right");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    return false;
                case ModificationMode.Move:
                    int step = getStep();
                    moveShape(step, -step);
                    break;
                case ModificationMode.Scale:
                    break;
                case ModificationMode.Rotate:
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleUP_LEFT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " up left");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    return false;
                case ModificationMode.Move:
                    int step = getStep();
                    moveShape(-step, -step);
                    break;
                case ModificationMode.Scale:
                    break;
                case ModificationMode.Rotate:
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleDOWN_RIGHT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " down right");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    return false;
                case ModificationMode.Move:
                    int step = getStep();
                    moveShape(step, step);
                    break;
                case ModificationMode.Scale:
                    break;
                case ModificationMode.Rotate:
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        private bool handleDOWN_LEFT()
        {
            if (emptyShapeForManipulationError()) return false;
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[MANIPULATION] " + Mode.ToString() + " down left");
            switch (Mode)
            {
                case ModificationMode.Unknown:
                    return false;
                case ModificationMode.Move:
                    int step = getStep();
                    moveShape(-step, step);
                    break;
                case ModificationMode.Scale:
                    break;
                case ModificationMode.Rotate:
                    break;
                default:
                    break;
            }
            sentTextFeedback(getDetailRegionFeedback(Mode));
            return true;
        }

        #endregion

        # region Fillmodes

        /// <summary>        
        /// Initializes the patters for filling forms.
        /// Patters are contained in OpenOffice extension TangramToolbar.oxt.
        /// Path C:\Users\voegler\AppData\Roaming\OpenOffice\4\user\uno_packages\cache\uno_packages\xxx\TangramToolbar.oxt\bitmap-pattern
        private void initializePatters()
        {
            string[] arrPatterns;
            try
            {
                string name = "";
                PatterDic.Add("no_pattern", "no_pattern");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                Logger.Instance.Log(LogPriority.DEBUG, "Pattern loader", "Application folder: " + appData);
#if LIBRE
                var foundDirs = Directory.GetDirectories(appData + "\\LibreOffice", "TangramToolbar_LO*.oxt", SearchOption.AllDirectories);
#else
                var foundDirs = Directory.GetDirectories(appData + "\\OpenOffice", "TangramToolbar_OO*.oxt", SearchOption.AllDirectories);
#endif

                Logger.Instance.Log(LogPriority.DEBUG, "Pattern loader", "directories inside application folder: " + (foundDirs != null ? foundDirs.Length.ToString() : "null"));
                if (foundDirs != null && foundDirs.Length > 0)
                {
                    var openOfficePath = foundDirs[foundDirs.Length - 1];
                    Logger.Instance.Log(LogPriority.DEBUG, "Pattern loader", "used directory: " + openOfficePath);
                    arrPatterns = Directory.GetFiles(openOfficePath, "*.png", SearchOption.AllDirectories);
                    //remove files named name_TS.png
                    foreach (string pattern in arrPatterns)
                    {
                        if (!pattern.ToLower().EndsWith("_ts.png"))  // only files without _ts.png
                        {
                            //add entry
                            name = Path.GetFileName(pattern).Replace(".png", "");
                            PatterDic.Add(name, pattern);
                            Logger.Instance.Log(LogPriority.DEBUG, "Pattern loader", "add pattern " + name);
                        }
                    }
                }
                if (PatterDic.Count == 0)
                {
                    AudioRenderer.Instance.PlaySoundImmediately(LL.GetTrans("tangram.oomanipulation.no_Tangram_extension_found"));
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Unhandled exception occurred while loading pattern from extension TangramToolbar.oxt", ex);
                Logger.Instance.Log(LogPriority.DEBUG, "Pattern loader", "[ERROR] can't load patterns: " + ex);
            }
        }

        private void changeFillStyle(int p)
        {
            if (LastSelectedShape != null && LastSelectedShape.IsValid() && PatterDic.Count > 0)
            {
                string bitmapName = LastSelectedShape.GetBackgroundBitmapName();

                if (fillStyleNum <= PatterDic.Count - 1 && fillStyleNum >= 0)
                {
                    fillStyleNum += p;
                }
                if (fillStyleNum < 0)
                {
                    fillStyleNum = PatterDic.Count - 1;
                }
                if (fillStyleNum > PatterDic.Count - 1)
                {
                    fillStyleNum = 0;
                }

                bitmapName = PatterDic.Keys.ElementAt(fillStyleNum);
                if (bitmapName == "no_pattern")
                {
                    LastSelectedShape.FillStyle = tud.mci.tangram.util.FillStyle.NONE;
                }
                else
                {
                    LastSelectedShape.FillStyle = tud.mci.tangram.util.FillStyle.BITMAP;
                }
                LastSelectedShape.SetBackgroundBitmap(PatterDic[bitmapName], tud.mci.tangram.util.BitmapMode.REPEAT, bitmapName);
                string pattern = bitmapName.Replace("_", " ");
                AudioRenderer.Instance.PlaySoundImmediately(pattern);
                sentTextFeedback(LL.GetTrans("tangram.oomanipulation.current_fill_pattern", pattern));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("changeFillStyle cannot changed some value are null or empty");
            }
        }

        #endregion

        #region line styles

        private void changeLineStyle(int p)
        {
            if (LastSelectedShape != null)
            {
                string lineStyleName = "";

                if (lineStyleNum <= linestyleNames.Length - 1 && lineStyleNum >= 0)
                {
                    lineStyleNum += p;
                }
                if (lineStyleNum < 0)
                {
                    lineStyleNum = linestyleNames.Length - 1;
                }
                else if (lineStyleNum > linestyleNames.Length - 1)
                {
                    lineStyleNum = 0;
                }
                lineStyleName = linestyleNames[lineStyleNum];


                //if (LastSelectedShape.Children.Count > 0) // group object
                //{
                //    AudioRenderer.Instance.PlaySoundImmediately("Ändern der Linie nicht möglich");
                //    return;
                //}

                switch (lineStyleName)
                {
                    case "solid":
                        solidStyle();
                        AudioRenderer.Instance.PlaySoundImmediately(LL.GetTrans("tangram.oomanipulation.linestyle.solid"));
                        break;
                    case "dashed_line":
                        dashedStyle();
                        AudioRenderer.Instance.PlaySoundImmediately(LL.GetTrans("tangram.oomanipulation.linestyle.dashed"));
                        break;
                    case "dotted_line":
                        dottedStyle();
                        AudioRenderer.Instance.PlaySoundImmediately(LL.GetTrans("tangram.oomanipulation.linestyle.dotted"));
                        break;
                    default:
                        break;
                }
            }
        }

        private string getCurrentLineStyle()
        {
            if (LastSelectedShape != null)
            {
                string lineStyle = LastSelectedShape.GetLineStyleName();
                switch (lineStyle)
                {
                    case "solid":
                        return LL.GetTrans("tangram.oomanipulation.linestyle.solid");
                    case "dashed_line":
                        return LL.GetTrans("tangram.oomanipulation.linestyle.dashed");
                    case "dotted_line":
                        return LL.GetTrans("tangram.oomanipulation.linestyle.dotted");
                }
            }
            return "";
        }


        private void changeLineWidth(int p)
        {
            // test whether style dotted if yes update dashStyle
            if (LastSelectedShape != null)
            {

                short dots = 0;
                short dashes = 0;
                int dotLen = 0;
                int dashLen = 0;
                int distance = 0;
                int oldLineWidth = LastSelectedShape.LineWidth;
                int newLinewidth = Math.Max(0, oldLineWidth + p);
                bool error = false;
                if (oldLineWidth == newLinewidth)
                {
                    error = true;
                }
                else
                {
                    tud.mci.tangram.util.DashStyle dashstyles = tud.mci.tangram.util.DashStyle.RECT;
                    LastSelectedShape.GetLineDash(out dashstyles, out dots, out dotLen, out dashes, out dashLen, out distance);
                    LastSelectedShape.LineWidth = LastSelectedShape.LineWidth + p;
                    if (dots > 0 && LastSelectedShape.LineStyle != tud.mci.tangram.util.LineStyle.SOLID)
                    {
                        dottedStyle();
                    }
                }

                float lineWidth = (float)LastSelectedShape.LineWidth;
                string message = LL.GetTrans("tangram.oomanipulation.mm", ((float)(lineWidth / 100)).ToString());
                if (error)
                {
                    AudioRenderer.Instance.PlayWaveImmediately(StandardSounds.End);
                    AudioRenderer.Instance.PlaySound(message);
                }
                else
                {
                    AudioRenderer.Instance.PlaySoundImmediately(message);
                }
            }
        }

        private void solidStyle()
        {
            if (LastSelectedShape != null)
            {
                LastSelectedShape.LineStyle = tud.mci.tangram.util.LineStyle.SOLID;
            }
        }

        private void dashedStyle()
        {
            if (LastSelectedShape != null)
            {
                LastSelectedShape.LineStyle = tud.mci.tangram.util.LineStyle.DASH;
                LastSelectedShape.SetLineDash(tud.mci.tangram.util.DashStyle.ROUND, 0, 0, 1, 1000, 500);
            }
        }

        private void dottedStyle()
        {
            if (LastSelectedShape != null)
            {
                LastSelectedShape.LineStyle = tud.mci.tangram.util.LineStyle.DASH;
                int linewidth = LastSelectedShape.LineWidth;
                LastSelectedShape.SetLineDash(tud.mci.tangram.util.DashStyle.ROUND, 1, linewidth, 0, 0, 500);
            }
        }

        #endregion

        #region Moving

        private void moveShapeHorizontal(int steps)
        {
            moveShape(steps, 0);
        }

        private void moveShapeVertical(int steps)
        {
            moveShape(0, steps);
        }

        private void moveShape(int horizontalSteps, int verticalSteps)
        {
            if (LastSelectedShapePolygonPoints != null)
            {
                int i;
                var point = LastSelectedShapePolygonPoints.Current(out i);
                point.X += horizontalSteps;
                point.Y += verticalSteps;
                var successs = LastSelectedShapePolygonPoints.UpdatePolyPointDescriptor(point, i, true);
                //successs = LastSelectedShapePolygonPoints.WritePointsToPolygon();

                if (successs) { playEdit(); }
                else { playError(); }
                return;
            }
            if (LastSelectedShape != null)
            {
                var pos = LastSelectedShape.Position;
                if (!pos.IsEmpty)
                {
                    pos.X += horizontalSteps;
                    pos.Y += verticalSteps;
                    LastSelectedShape.Position = pos;
                    playEdit();
                }
            }
        }

        #endregion

        #region Scaling

        private const int minSize = 600;

        private void scaleWidth(int steps) { scale(steps, 0); }

        private void scaleHeight(int steps) { scale(0, steps); }

        private void scale(int widthSteps, int heightSteps)
        {
            //TODO: resolve the problem, when shape is rotated - then you have to switch width and height?!

            if (LastSelectedShape != null)
            {
                bool error = false;
                var size = LastSelectedShape.Size;

                size.Width += widthSteps;
                size.Height += heightSteps;

                if (size.Height < minSize)
                {
                    size.Height = minSize;
                    heightSteps = minSize - LastSelectedShape.Size.Height;
                    error = true;
                }

                if (size.Width < minSize)
                {
                    size.Width = minSize;
                    widthSteps = minSize - LastSelectedShape.Size.Width;
                    error = true;
                }
                var pos = LastSelectedShape.Position;
                pos.X -= (widthSteps / 2);
                pos.Y -= (heightSteps / 2);

                if (pos.X < 0) pos.X = 0;
                if (pos.Y < 0) pos.Y = 0;

                LastSelectedShape.Size = size;
                LastSelectedShape.Position = pos;

                if (error) playError();
                else playEdit();
            }
        }

        #endregion

        #region Rotating

        private void rotateLeft(int degres) { rotate(degres); }

        private void rotateRight(int degres) { rotate(-degres); }

        private void rotate(int degres)
        {
            if (LastSelectedShape != null)
            {
                int rotation = LastSelectedShape.Rotation;
                LastSelectedShape.Rotation = (rotation + degres) - (rotation % 100);
                playEdit();
                int rot = (LastSelectedShape.Rotation / 100);
                String detail = LL.GetTrans("tangram.oomanipulation.rotated", rot.ToString("0."));
                sendDetailInfo(detail);
                play(LL.GetTrans("tangram.oomanipulation.degrees", rot.ToString("0.")));
            }
        }

        #endregion


        #endregion

        #region audio and detail region feedback

        private String getAudioFeedback(ModificationMode mode)
        {
            String audio = "";//"Element ";

            switch (mode)
            {
                case ModificationMode.Unknown:
                    break;
                case ModificationMode.Move:
                    audio += LL.GetTrans("tangram.oomanipulation.manipulation.move");
                    if (LastSelectedShape != null && LastSelectedShape.Position != null)
                    {
                        audio += ", " + LL.GetTrans("tangram.oomanipulation.current") + ": "
                            + LL.GetTrans("tangram.oomanipulation.manipulation.move.status.audio"
                            , ((float)((float)LastSelectedShape.Position.X / 1000)).ToString("0.#")
                            , ((float)((float)LastSelectedShape.Position.Y / 1000)).ToString("0.#"));
                    }
                    break;
                case ModificationMode.Scale:
                    audio += LL.GetTrans("tangram.oomanipulation.manipulation.scale");
                    if (LastSelectedShape != null && LastSelectedShape.Size != null)
                    {
                        audio += ", " + LL.GetTrans("tangram.oomanipulation.current") + ": "
                            + LL.GetTrans("tangram.oomanipulation.manipulation.scale.status.audio"
                            , ((float)((float)LastSelectedShape.Size.Height / 1000)).ToString("0.#")
                            , ((float)((float)LastSelectedShape.Size.Width / 1000)).ToString("0.#"));
                    }
                    break;
                case ModificationMode.Rotate:
                    audio += LL.GetTrans("tangram.oomanipulation.manipulation.rotate");
                    if (LastSelectedShape != null)
                    {
                        audio += ", " + LL.GetTrans("tangram.oomanipulation.current") + ": "
                            + LL.GetTrans("tangram.oomanipulation.manipulation.rotate.status.audio"
                            , (LastSelectedShape.Rotation / 100).ToString("0."));
                    }
                    break;
                case ModificationMode.Fill:
                    audio += LL.GetTrans("tangram.oomanipulation.manipulation.filling.audio");
                    if (LastSelectedShape != null)
                    {
                        if (LastSelectedShape.GetProperty("FillBitmap") == null) audio += ", " + LL.GetTrans("tangram.oomanipulation.manipulation.filling.status.none");
                        else
                        {
                            audio += ", " + LL.GetTrans("tangram.oomanipulation.current") + ": "
                                + LL.GetTrans("tangram.oomanipulation.manipulation.filling.status"
                                , LastSelectedShape.GetBackgroundBitmapName());
                        }
                    }
                    break;
                case ModificationMode.Line:
                    audio += LL.GetTrans("tangram.oomanipulation.manipulation.line.audio");
                    if (LastSelectedShape != null)
                    {
                        audio += ", " + LL.GetTrans("tangram.oomanipulation.current") + ": "
                            + LL.GetTrans("tangram.oomanipulation.manipulation.line.status.audio"
                            , ((float)((float)LastSelectedShape.LineWidth / 100)).ToString("0.#")
                            , getCurrentLineStyle());
                    }
                    break;
                default:
                    break;
            }
            return audio;
        }

        private String getDetailRegionFeedback(ModificationMode mode)
        {
            String detail = "";//Bearbeitung: ";

            switch (mode)
            {
                case ModificationMode.Unknown:
                    break;
                case ModificationMode.Move:

                    if (LastSelectedShapePolygonPoints != null) { return GetPointText(LastSelectedShapePolygonPoints); }

                    detail += LL.GetTrans("tangram.oomanipulation.manipulation.move");
                    if (LastSelectedShape != null && LastSelectedShape.Position != null)
                    {
                        detail += " - " + LL.GetTrans("tangram.oomanipulation.manipulation.move.status"
                            , ((float)((float)LastSelectedShape.Position.X / 1000)).ToString("0.#")
                            , ((float)((float)LastSelectedShape.Position.Y / 1000)).ToString("0.#"));
                    }
                    break;
                case ModificationMode.Scale:
                    detail += LL.GetTrans("tangram.oomanipulation.manipulation.scale");
                    if (LastSelectedShape != null && LastSelectedShape.Size != null)
                    {
                        detail += " - " + LL.GetTrans("tangram.oomanipulation.manipulation.scale.status"
                            , ((float)((float)LastSelectedShape.Size.Height / 1000)).ToString("0.#")
                            , ((float)((float)LastSelectedShape.Size.Width / 1000)).ToString("0.#"));
                    }
                    break;
                case ModificationMode.Rotate:
                    detail += LL.GetTrans("tangram.oomanipulation.manipulation.rotate");
                    if (LastSelectedShape != null)
                    {
                        detail += " - " + LL.GetTrans("tangram.oomanipulation.degrees", (LastSelectedShape.Rotation / 100).ToString("0."));
                    }
                    break;
                case ModificationMode.Fill:
                    detail += LL.GetTrans("tangram.oomanipulation.manipulation.filling");
                    if (LastSelectedShape != null)
                    {
                        if (LastSelectedShape.GetProperty("FillBitmap") != null)
                        {
                            detail += " - " + LastSelectedShape.GetBackgroundBitmapName(); // TODO: incorrect name of bitmap after changing it the first time (only returns "Bitmape 1" ect.)
                        }
                    }
                    break;
                case ModificationMode.Line:
                    detail += LL.GetTrans("tangram.oomanipulation.manipulation.line");
                    if (LastSelectedShape != null)
                    {
                        detail += " - " + LL.GetTrans("tangram.oomanipulation.manipulation.line.status"
                            , ((float)((float)LastSelectedShape.LineWidth / 100)).ToString("0.#")
                            , getCurrentLineStyle());
                    }
                    break;
                default:
                    break;
            }
            return detail;
        }

        #endregion

    }
}