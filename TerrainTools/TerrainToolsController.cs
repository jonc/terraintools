/*
 * Copyright (c) Contributors http://forge.opensimulator.org/gf/project/terraintools/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.IO;
using log4net;
using System.Reflection;
using OpenSim.Region.Framework.Interfaces;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework.Console;

namespace TerrainTools
{
    /// <summary>
    /// Controller for the Terrain Tools Module
    /// It Is fed with a list of WorkItems to process
    /// and decides what is best to do with each one
    /// 
    /// Generally - check validity of the incoming command 
    /// and if it seems actionable update the Sim.
    /// </summary>
	public class TerrainToolsController
	{
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //Implementation of the tools provided by this module
        private TerrainTools m_tools;

        public TerrainToolsController(TerrainTools model)
        {
            m_tools = model;
            InstallCommands();
        }


        #region initialisation


        private void fn(string module, string[] args)
        {
            Console.WriteLine("Wirks");
        }


        private void AddCommand(string cmd, string bnf, string help, CommandDelegate fn)
        {
            Commands cmds = MainConsole.Instance.Commands;
            cmds.AddCommand("terrain-tools", true, "terrain-tools " + cmd, "terrain-tools " + cmd + " " + bnf, help, fn);

        }

        /// <summary>
        /// Tell the commander which commands we understand
        /// currently this is load, save and stitch
        /// </summary>
        private void InstallCommands()
        {
            AddCommand("load", "<filename>", "Tiles all the Regions in the Simulator from the passed in file", InterfaceLoadAll);
            AddCommand("save", "<filename>", "Saves the HeightMap for all the Regions in the Simulator as a single file", InterfaceSaveAll);
            AddCommand("stitch", "<depth>", "smooths the edges of the heightmaps between all the regions in this Sim", InterfaceStitchAll);
            AddCommand("split", "<filename>", "saves all the heightmaps of the regions hosted by this sim as individual files", InterfaceSplitAll);
            AddCommand("load-part", "<filename> <num regions X>, <num regions Y> <X start> <Y start>", "Loads a terrain from a section of a larger file.",InterfaceLoadTileFile);
            AddCommand("save-part", "<filename> <num regions X>, <num regions Y> <X start> <Y start>", "Saves a number of regions terrains to a single file.", InterfaceSaveTileFile);
            AddCommand("stitch-part", "<depth> <num regions X>, <num regions Y> <X start> <Y start>", "smooths the edges of a number of regions", InterfaceStitch);
            AddCommand("split-part", "<filename> <num regions X>, <num regions Y> <X start> <Y start>", "save the heightmaps in the area specified as individual files", InterfaceSplitPart);
            AddCommand("test", "<filename>", "checks if a terrain file is valid", InterfaceTestFile);
            AddCommand("convert", "<source file> <destination file>", "converts a terrain file to a different file type", InterfaceConvertFile);
            AddCommand("rescale", "<min elevation> <max elevation>", "rescales the heightmap of the regions hosted in this sim",  InterfaceRescale);

        }


        #endregion initialisation

        #region commandparsing


        private void SendErrorResponse(string text)
        {
            MainConsole.Instance.Output(text);
        }

        private void SendResponse(string text)
        {
            MainConsole.Instance.Output(text);
        }


        /// <summary>
        /// Parse a 'split' command and invoke functionality
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceSplitAll(string module, string[] args)
        {
            String filenamePattern = args[2];
            FileInfo file = new FileInfo(filenamePattern);
            if (FileUtils.CanWriteFile(file) == false)
            {
                SendErrorResponse("Cannot write files to directory " + Path.GetDirectoryName(filenamePattern));
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("No loader is registered for files of type " + file.Extension);
            }
            else
            {
                m_tools.SplitAll(file);
            }

        }


        /// <summary>
        /// Parse a 'split-part' command and invoke functionality
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceSplitPart(string module, string[] args)
        {
             String filenamePattern = args[2];
            int numX = Convert.ToInt32(args[3]);
            int numY = Convert.ToInt32(args[4]);
            int startX = Convert.ToInt32(args[5]);
            int startY = Convert.ToInt32(args[6]);


            FileInfo file = new FileInfo(filenamePattern);
            if (FileUtils.CanWriteFile(file) == false)
            {
                SendErrorResponse("Cannot write files to directory " + Path.GetDirectoryName(filenamePattern));
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("No loader is registered for files of type " + file.Extension);
            }
            else if (m_tools.CheckDimensionsAreValid(numX, numY, startX, startY) == false)
            {
                SendErrorResponse("The specified parameters exceed the bounds of the regions hosted by this sim");
            }

            else
            {
                m_tools.SplitPart(numX, numY, startX, startY, file);
            }

        }


        /// <summary>
        /// Parse the 'convert' command.
        /// Use the appropriate Terrain Loaders to read in the 'from' file 
        /// and write is out again as the 'to' file. Changing its format as dictated by the 
        /// extension of the second file
        /// 
        /// Note: this utility does not affect the heightmaps loaded in the sim.
        /// </summary>
        /// <param name="args">the parameters passed into this command</param>
        private void InterfaceConvertFile(string module, string[] args)
        {
            String fromName = args[2];
            String toName = args[3];
            FileInfo from = new FileInfo(fromName);
            FileInfo to = new FileInfo(toName);

            if (FileUtils.CanReadFromFile(from) == false)
            {
                SendErrorResponse("File to read from does not exist");
            }
            else if (FileUtils.CanWriteFile(to) == false)
            {
                SendErrorResponse("Cannot write the output file");
            }
            else if (m_tools.IsLoaderRegisteredForFile(from) == false)
            {
                SendErrorResponse("Cannot parse the input file, no loader is registered for files of this type");
            }
            else if (m_tools.IsLoaderRegisteredForFile(to) == false)
            {
                SendErrorResponse("Cannot write the output file, no loader is registered for files of this type");
            }
            else
            {
                m_tools.ConvertTerrainFile(from, to);
            }
        }

        /// <summary>
        /// Parse a 'test' command and invoke functionality
        /// 
        /// Note: this utility does not affect the heightmaps loaded in the sim.
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceTestFile(string module, string[] args)
        {
            String filename = args[2];
            FileInfo file = new FileInfo(filename);
            if (FileUtils.CanReadFromFile(file))
            {
                TestFile(filename);
            }
            else
            {
                SendErrorResponse("File does not exist");
            }
        }



        /// <summary>
        /// Parse a 'load' command and invoke functionality
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceLoadAll(string module, string[] args)
        {
            String filename = args[2];
            FileInfo file = new FileInfo(filename);
            if (FileUtils.CanReadFromFile(file) == false)
            {
                SendErrorResponse("The file does not exist");
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("No loader is registered for files of this type");
            }
            else if (!m_tools.IsRectangularSetOfRegionsInSim())
            {
                SendErrorResponse("Regions in this sim do not form a contiguous, rectangular shape, consider using the 'load-part' command instead");
            }
            else
            {
                bool retVal = m_tools.LoadAll(file);
                if (retVal == false)
                {
                    SendErrorResponse("File is the wrong size to tile all the regions in this sim, consider using the 'load-part' command instead");
                }
            }
        }


        /// <summary>
        /// Parse a 'save' command and invoke functionality
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceSaveAll(string module, string[] args)
        {
            String filename = args[2];
            FileInfo file = new FileInfo(filename);

            if (FileUtils.CanWriteFile(file) == false)
            {
                SendErrorResponse("Cannot write to file");
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("No loader is registered for files of this type");
            }
            else
            {
                bool retVal = m_tools.SaveAll(file);
                if (retVal == false)
                {
                    SendErrorResponse("The regions in this sim do not form a contiguous, rectangular shape, consider using the 'save-part' command instead");
                }
            }
        }



        /// <summary>
        /// Parse a 'stitch' command and invoke functionality
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceStitchAll(string module, string [] args)
        {
            int width = Convert.ToInt32(args[2]);
            m_tools.StitchAll(width);
        }


        /// <summary>
        /// Parse a 'stitch-part' command, and invoke functionality if parameters are valid
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceStitch(string module, string[] args)
        {
            int width = Convert.ToInt32(args[2]);
            int numX = Convert.ToInt32(args[3]);
            int numY = Convert.ToInt32(args[4]);
            int startX = Convert.ToInt32(args[5]);
            int startY = Convert.ToInt32(args[6]);

            if (m_tools.CheckDimensionsAreValid(numX, numY, startX, startY) == false)
            {
                SendErrorResponse("The parameters exceed the bounds of the regions hosted in this sim");
            }
            else
            {
                m_tools.StitchRegions(width, numX, numY, startX, startY);
            }
        }

        /// <summary>
        /// Parse a 'save-part' command and invoke functionality if parameters are valid
        /// </summary>
        /// <param name="args">parameters passed into this command</param>
        private void InterfaceSaveTileFile(string module, string[] args)
        {
            string filename = args[2];
            int numX = Convert.ToInt32(args[3]);
            int numY = Convert.ToInt32(args[4]);
            int startX = Convert.ToInt32(args[5]);
            int startY = Convert.ToInt32(args[6]);

            FileInfo file = new FileInfo(filename);

            if (m_tools.CheckDimensionsAreValid(numX, numY, startX, startY) == false)
            {
                SendErrorResponse("The parameters exceed the bounds of the regions hosted in this sim");
            }
            else if (FileUtils.CanWriteFile(file) == false)
            {
                SendErrorResponse("The file cannot be written to");
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("Cannot write file, no loader is registered for files of this type");
            }
            else
            {
                m_tools.SaveRegionsToFile(file, numX, numY, startX, startY);
            }
        }

        /// <summary>
        /// parse a 'load-part' command and invoke functionality if parameter are valid
        /// </summary>
        /// <param name="args">the parameters passed into  this command</param>
        private void InterfaceLoadTileFile(string module, string[] args)
        {
            string filename = args[2];
            int numX = Convert.ToInt32(args[3]);
            int numY = Convert.ToInt32(args[4]);
            int startX = Convert.ToInt32(args[5]);
            int startY = Convert.ToInt32(args[6]);

            FileInfo file = new FileInfo(filename);


            if (m_tools.CheckDimensionsAreValid(numX, numY, startX, startY) == false)
            {
                SendErrorResponse("The parameters exceed the bounds of the regions hosted in this sim");
            }
            else if (FileUtils.CanReadFromFile(file) == false)
            {
                SendErrorResponse("The file does not exist");
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("Cannot write file, no loader is registered for files of this type");
            }
            else
            {
                m_tools.LoadRegionsFromFile(file, numX, numY, startX, startY);
            }
        }


        private void InterfaceRescale(string module, string[] args)
        {
            double desiredMin = Convert.ToDouble(args[2]);
            double desiredMax = Convert.ToDouble(args[3]);

            if (desiredMax < desiredMin)
            {
                SendErrorResponse("Invalid parameters, Max Value is less than Min Value");
            }
            else
            {
                m_tools.Rescale(desiredMin, desiredMax);
            }
        }

        /// <summary>
        /// Test the filename.
        /// Check it exists
        /// Check it has an extension we recognise
        /// Determine if our registered loader can load it
        /// report its dimensions
        /// </summary>
        /// <param name="filename">the terrain file we are going to check</param>
        private void TestFile(string filename)
        {
            FileInfo file = new FileInfo(filename);
            if (FileUtils.CanReadFromFile(file) == false)
            {
                SendErrorResponse("The file does not exist");
            }
            else if (m_tools.IsLoaderRegisteredForFile(file) == false)
            {
                SendErrorResponse("No loader is registered for files of this type");
            }
            else
            {
                if (m_tools.IsWholeNumberOfRegions(file) == false)
                {
                    SendErrorResponse("File " + filename + " Invalid: The file does not tile a whole number of regions");
                }
                else
                {
                    int width;
                    int height;
                    if (m_tools.DetermineFileSize(file, out width, out height) == false)
                    {
                        SendErrorResponse("Invalid state: cannot determine the file size");
                    }
                    else
                    {
                        SendResponse("File " + filename + " can be loaded by the Terrain Tools. It will tile W=" + width + ", H=" + height + " regions");
                    }
                }
            }
        }

#endregion commandparsing

    }
}
