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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.CoreModules.World.Terrain.FloodBrushes;
using OpenSim.Region.CoreModules.World.Terrain.FileLoaders;
using TerrainTools.FileLoaders;

namespace TerrainTools
{
    /// <summary>
    /// Implements a number of Terrain Manipulation Functions
    /// </summary>
	public class TerrainTools
	{
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly int HALF_REGION_SIZE = (int)Constants.RegionSize / 2;

        /// all the scenes hosted in this simulator
        private List<Scene> m_scenes = new List<Scene>();

        //the terrain loaders we can use
        private readonly Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader>();

        //FloodBrush used to smooth the joins between regions
        private SmoothArea m_smoother = new SmoothArea();

        public TerrainTools()
        {
            InstallPlugins();

        }

        /// <summary>
        /// Add a region
        /// </summary>
        /// <param name="scene"></param>
        public void AddRegion(Scene scene)
        {
            m_scenes.Add(scene) ;
        }



        /// <summary>
        /// Is there an FileLoader registered that can handle the filetype of the passed in file
        /// </summary>
        /// <param name="file">The file to find a loader for</param>
        /// <returns></returns>
        public bool IsLoaderRegisteredForFile(FileInfo file)
        {
            return m_loaders.ContainsKey(file.Extension);
        }


        public void SplitAll(FileInfo file)
        {
            int numX, numY, startX, startY;
            DetermineSimBounds(out numX, out numY, out startX, out startY);
            Scene[,] scenes = GetRegionsToTile(startX, startY, numX, numY);
            Split(file, GetLoaderForFile(file), scenes);
        }


        public void SplitPart(int numX, int numY, int startX, int startY, FileInfo file)
        {
            Scene[,] scenes = GetRegionsToTile(startX, startY, numX, numY);
            Split(file, GetLoaderForFile(file), scenes);
        }


        /// <summary>
        /// For all of the passed in regions, save each region to a separate terrain file based on the 
        /// passed in stem using the given loader
        /// Append the grid coordinates to the stem to generate a unique filename
        /// </summary>
        /// <param name="file">the file to form the stem of the written tiles</param>
        /// <param name="extension"></param>
        private void Split(FileInfo file, ITerrainLoader loader, Scene[,] scenes)
        {
            String stem = Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.FullName));
            String extension = file.Extension;

            foreach (Scene scene in scenes)
            {
                //split command doesn't rely on a rectangular, contiguous set
                //of regions... so have to check each one
                if (scene != null)
                {
                    String filename = GenerateFilenameForRegion(stem, extension, scene);
                    loader.SaveFile(filename, scene.Heightmap);
                }
            }
        }

        private static String GenerateFilenameForRegion(String stem, String extension, Scene scene)
        {
            RegionInfo info = scene.RegionInfo;
            String location = "-" + info.RegionLocX + "-" + info.RegionLocY;
            return stem + location + extension;
        }






        /// <summary>
        /// Determine if the set of regions described by the parameters to this method are
        /// all hosted within scenes managed by this sim
        /// </summary>
        /// <param name="startX">X bound of the first region to include</param>
        /// <param name="startY">Y bound of the first region to include</param>
        /// <param name="numX">X extent of this subset in number of regions</param>
        /// <param name="numY">Y extent of this subset in number of regions</param>
        /// <returns>true if all region locations described by parameters are hosted within this sim, false otherwise</returns>
        public bool CheckDimensionsAreValid(int numX, int numY, int startX, int startY)
        {
            bool retVal = true;

            Scene[,] scenes = GetRegionsToTile(startX, startY, numX, numY);
            foreach (Scene scene in scenes)
            {
                if (scene == null)
                {
                    retVal = false;
                    break;
                }
            }

            return retVal;
        }


        /// <summary>
        /// Given a file, determine which of our registered loaders is appropriate to handle load/save operations against it
        /// </summary>
        /// <param name="filename">the extension of which will determine the appropriate loader</param>
        /// <returns>the appropriate FileLoader or null if none can be found</returns>
        public ITerrainLoader GetLoaderForFile(FileInfo file)
        {
            ITerrainLoader retVal = null;
            if (m_loaders.ContainsKey(file.Extension))
            {
                retVal = m_loaders[file.Extension];
            }
            return retVal;
        }


        public bool DetermineFileSize(FileInfo file, out int width, out int height)
        {
            bool retVal = true;
            String extension = file.Extension;
            ITerrainLoader loader = GetLoaderForFile(file);
            ITerrainChannel heightmap = loader.LoadFile(file.FullName);
            width = 0;
            height = 0;

            if (loader.GetType().IsSubclassOf(new GenericSystemDrawing().GetType()))
            {
                //this is a bitmap so it knows how big it is
                width =  heightmap.Width / (int)Constants.RegionSize;
                height = heightmap.Height / (int)Constants.RegionSize;
            }
            else if (extension.ToLower() == ".r32" || extension.ToLower() == ".f32")
            {
                retVal = SizeL3DTFile(file, out width, out height);
            }
            else if (extension.ToLower() == ".raw")
            {
                retVal = SizeLLRawFile(file, out width, out height);
            }
            else
            {
                retVal = false; // we can't determine the file type in order to size it
            }

            return retVal;
        }

        private bool SizeLLRawFile(FileInfo file, out int width, out int height)
        {
            int bytesPerHeight = 13; // SL raw files have 13 bytes per heightmap point
            return SizeUnsizedFile(file, out width, out height, bytesPerHeight);
        }

        private bool SizeL3DTFile(FileInfo file, out int width, out int height)
        {
            int bytesPerHeight = sizeof(float); // L3DT RAW files are float arrays
            return SizeUnsizedFile(file, out width, out height, bytesPerHeight);
        }

        private bool SizeUnsizedFile(FileInfo file, out int width, out int height, int bytesPerHeight)
        {
            bool retVal = true;

            width = 0;
            height = 0;

            // work out how many we have and fit to a rectangular shape
            double numRegions = file.Length / (double)(Constants.RegionSize * Constants.RegionSize * bytesPerHeight);

            if (IsInt(numRegions))
            {
                double side = Math.Sqrt(numRegions);

                if (IsInt(side))
                {
                    width = (int)side;
                    height = (int)side;
                    retVal = true;
                }
                else
                {
                    //TODO: finish this
                    m_log.Error("File " + file.Name + " Invalid: not a square - currently unhandled");
                    retVal = false;
                }

            }
            else
            {
                m_log.Error("File " + file.Name + " Invalid: its size is not a multiple of the Simulator Region size");
                retVal = false;
            }

            return retVal;

        }

        private bool IsInt(double numRegions)
        {
            bool retVal = true;
            try
            {
                int i = Convert.ToInt32(numRegions);
                if ((double)i != numRegions)
                {
                    retVal = false;
                }
            }
            catch (Exception)
            {
                retVal = false;
            }

            return retVal;

        }


        public void StitchAll(int width)
        {
            int numX, numY, startX, startY;
            DetermineSimBounds(out numX, out numY, out startX, out startY);
            StitchRegions(width, numX, numY, startX, startY);
        }



        /// <summary>
        /// smooth the edges between the set of regions covered by the passed in parameters
        /// 
        /// </summary>
        /// <param name="width">how far back into the region to include in the smoothing</param>
        /// <param name="numX">number of regions in the X direction that form this set</param>
        /// <param name="numY">number of regions in the Y direction that form this set</param>
        /// <param name="startX">X location of the first region to include in this set</param>
        /// <param name="startY">Y location of the first region to include in this set</param>
        public void StitchRegions(int width, int numX, int numY, int startX, int startY)
        {
            //work out which of the regions in the sim we are tiling here
            Scene[,] regions = GetRegionsToTile(startX, startY, numX, numY);

            // smooth all the east-west edges in the chosen area
            for (int y = 0; y < numY; y++)
            {
                // get the next two adjoining regions and stitch them
                for (int x = 1; x < numX; x++)
                {
                    Scene westRegion = regions[x - 1, y];
                    Scene eastRegion = regions[x, y];
                    // note we don't have to be rectangular and contiguous to stitch effectively
                    if (westRegion != null && eastRegion != null)
                    {
                        m_log.Debug("[TERRAIN TOOLS] Stitching regions " + westRegion.RegionInfo.RegionName + " and " + eastRegion.RegionInfo.RegionName);
                        StitchEastWest(width, ref westRegion.Heightmap, ref eastRegion.Heightmap);
                    }
                }
            }

            //smooth all the north-south edges in the chosen area
            for (int x = 0; x < numX; x++)
            {
                // get the next two adjoining regions and stitch them
                for (int y = 1; y < numY; y++)
                {
                    Scene southRegion = regions[x, y - 1];
                    Scene northRegion = regions[x, y];
                    // shouldn't rely on a contiguous rectangular area here
                    if (southRegion != null && northRegion != null)
                    {
                        m_log.Debug("[TERRAIN TOOLS] Stitching regions " + southRegion.RegionInfo.RegionName + " and " + northRegion.RegionInfo.RegionName);
                        StitchNorthSouth(width, ref southRegion.Heightmap, ref northRegion.Heightmap);
                    }
                }
            }

            UpdateTaintedRegions();

        }

        private void StitchNorthSouth(int width, ref ITerrainChannel southRegion, ref ITerrainChannel northRegion)
        {
            //make a new HeightMap from the bottom half of the north region and the top half of the south region
            TerrainChannel tempMap = new TerrainChannel();
            for (int x = 0; x < tempMap.Width; x++)
            {
                for (int y = 0; y < tempMap.Height; y++)
                {
                    tempMap[x, y] = (y < HALF_REGION_SIZE) ? southRegion[x, y + HALF_REGION_SIZE] : northRegion[x, y - HALF_REGION_SIZE];
                }
            }

            bool[,] fillArea = new bool[tempMap.Width, tempMap.Height];
            for (int x = 0; x < tempMap.Width; x++)
            {
                for (int y = 0; y < tempMap.Height; y++)
                {
                    if (y > HALF_REGION_SIZE - width && y < HALF_REGION_SIZE + width)
                    {
                        fillArea[x, y] = true;
                    }
                    else
                    {
                        fillArea[x, y] = false;
                    }
                }
            }

            //smooth the join
            m_smoother.FloodEffect(tempMap, fillArea, 1.0d);

            //update our original maps from the smoothed temp map
            for (int x = 0; x < tempMap.Width; x++)
            {
                for (int y = 0; y < tempMap.Height; y++)
                {
                    if (y < HALF_REGION_SIZE)
                    {
                        southRegion[x, y + HALF_REGION_SIZE] = tempMap[x, y];
                    }
                    else
                    {
                        northRegion[x, y - HALF_REGION_SIZE] = tempMap[x, y];
                    }
                }
            }

        }

        private void StitchEastWest(int width, ref ITerrainChannel westRegion, ref ITerrainChannel eastRegion)
        {
            //make a new HeightMap from the right half of the west region and the left half of the east region
            TerrainChannel tempMap = new TerrainChannel();
            for (int y = 0; y < tempMap.Height; y++)
            {
                for (int x = 0; x < tempMap.Width; x++)
                {
                    tempMap[x, y] = (x < HALF_REGION_SIZE) ? westRegion[x + HALF_REGION_SIZE, y] : eastRegion[x - HALF_REGION_SIZE, y];
                }
            }

            bool[,] fillArea = new bool[tempMap.Width, tempMap.Height];
            for (int y = 0; y < tempMap.Height; y++)
            {
                for (int x = 0; x < tempMap.Width; x++)
                {
                    if (x > HALF_REGION_SIZE - width && x < HALF_REGION_SIZE + width)
                    {
                        fillArea[x, y] = true;
                    }
                    else
                    {
                        fillArea[x, y] = false;
                    }
                }
            }

            //smooth the join
            m_smoother.FloodEffect(tempMap, fillArea, 1.0d);

            //update our original maps from the smoothed temp map
            for (int y = 0; y < tempMap.Height; y++)
            {
                for (int x = 0; x < tempMap.Width; x++)
                {
                    if (x < HALF_REGION_SIZE)
                    {
                        westRegion[x + HALF_REGION_SIZE, y] = tempMap[x, y];
                    }
                    else
                    {
                        eastRegion[x - HALF_REGION_SIZE, y] = tempMap[x, y];
                    }
                }
            }
        }


        public bool SaveAll(FileInfo file)
        {
            int numX, numY, startX, startY;

            DetermineSimBounds(out numX, out numY, out startX, out startY);

            if (!IsRectangularSetOfRegionsInSim())
            {
                return false;
            }
            else
            {
                SaveRegionsToFile(file, numX, numY, startX, startY);
            }

            return true;
        }


        /// <summary>
        /// save the combined heightmap of an area encompassing a number of regions to a file
        /// a.k.a save-tile
        /// Command will only execute is there is a contiguous set of regions in this sim 
        /// within the area described by the parameters
        /// </summary>
        /// <param name="filename">filename to write the combined heightmap to, note extension determines the format</param>
        /// <param name="numX">number of regions in the X direction that form this set</param>
        /// <param name="numY">number of regions in the Y direction that form this set</param>
        /// <param name="startX">X location of the first region to include in this set</param>
        /// <param name="startY">Y location of the first region to include in this set</param>
        public void SaveRegionsToFile(FileInfo file, int numX, int numY, int startX, int startY)
        {

            // save the cross region heightmap to the given filename
            TerrainChannel tiledChannel = BuildCombinedHeightMap(numX, numY, startX, startY);
            ITerrainLoader loader = GetLoaderForFile(file);
            loader.SaveFile(file.FullName, tiledChannel);
        }

        private TerrainChannel BuildCombinedHeightMap(int numX, int numY, int startX, int startY)
        {
            //work out which of the regions in the sim we are tiling here
            Scene[,] regions = GetRegionsToTile(startX, startY, numX, numY);

            // build the height map across all of those regions
            TerrainChannel tiledChannel = new TerrainChannel((int)Constants.RegionSize * numX, (int)Constants.RegionSize * numY);
            for (int x = 0; x < tiledChannel.Width; x++)
            {

                for (int y = 0; y < tiledChannel.Height; y++)
                {
                    tiledChannel[x, y] = regions[x / Constants.RegionSize, y / Constants.RegionSize].Heightmap[x % (int)Constants.RegionSize, y % (int)Constants.RegionSize];
                }
            }

            return tiledChannel;
        }


        private void SaveRegionsToStream(int numX, int numY, int startX, int startY, Stream stream)
        {
            TerrainChannel tiledChannel = BuildCombinedHeightMap(numX, numY, startX, startY);
            //ITerrainLoader loader = GetLoaderForFile(new FileInfo("heightmap.bmp"));
            ITerrainLoader loader = m_loaders[".bmp"];
            loader.SaveStream(stream, tiledChannel);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool LoadAll(FileInfo file)
        {
            int numX, numY, startX, startY, simX, simY;
            DetermineFileSize(file, out numX, out numY);    // ignore the return value, if we cant work out the size
            // then we'll attempt to load it anyway
            DetermineSimBounds(out simX, out simY, out startX, out startY);
            if (CheckCorrectSize(numX, numY, simX, simY) == false)
            {
                return false;
            }
            else
            {
                LoadRegionsFromFile(file, numX, numY, startX, startY);
            }

            return true;
        }



        /// <summary>
        /// load a single heightmap and apply it across a number of the regions hosted within this simulator
        /// a.k.a load-tile
        /// Command will only execute is there is a contiguous set of regions in this sim 
        /// within the area described by the parameters
        /// </summary>
        /// <param name="filename">filename to load the combined heightmap from, note extension determines the format</param>
        /// <param name="numX">number of regions in the X direction that form this set</param>
        /// <param name="numY">number of regions in the Y direction that form this set</param>
        /// <param name="startX">X location of the first region to include in this set</param>
        /// <param name="startY">Y location of the first region to include in this set</param>
        public void LoadRegionsFromFile(FileInfo file, int numX, int numY, int startX, int startY)
        {
            ITerrainLoader loader = GetLoaderForFile(file);
            ITerrainChannel tiledChannel = LoadFileWithLoader(file, numX, numY, loader);

            // work out which regions we need to update the terrain for
            Scene[,] regions = GetRegionsToTile(startX, startY, numX, numY);

            // override the heightMap for each region in the tiled area
            for (int x = 0; x < tiledChannel.Width; x++)
            {
                for (int y = 0; y < tiledChannel.Height; y++)
                {
                    // if this part of the combined heightmap is within one of the regions
                    // we've been asked to update then update that regions heightmap
                    if (ShouldTileThisPoint(x, y, numX, numY))
                    {
                        regions[x / Constants.RegionSize, y / Constants.RegionSize].Heightmap[x % (int)Constants.RegionSize, y % (int)Constants.RegionSize] = tiledChannel[x, y];
                    }
                }
            }

            UpdateTaintedRegions();
        }

        /// <summary>
        /// Converts a terrain file to another format
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void ConvertTerrainFile(FileInfo from, FileInfo to)
        {
            int numX, numY;

            DetermineFileSize(from, out numX, out numY);

            ITerrainLoader fromLoader = GetLoaderForFile(from);
            ITerrainLoader toLoader = GetLoaderForFile(to);

            ITerrainChannel tempMap = LoadFileWithLoader(from, numX, numY, fromLoader);
            toLoader.SaveFile(to.FullName, tempMap);
        }



        internal void Rescale(double desiredMin, double desiredMax)
        {
            int numX, numY, startX, startY;

            // determine desired scaling factor
            double desiredRange = desiredMax - desiredMin;
            m_log.InfoFormat("Desired {0}, {1} = {2}", new Object [] {desiredMin, desiredMax, desiredRange});

            DetermineSimBounds(out numX, out numY, out startX, out startY);

            ITerrainChannel heightMap = BuildCombinedHeightMap(numX, numY, startX, startY);
            // work out which regions we need to update the terrain for
            Scene[,] regions = GetRegionsToTile(startX, startY, numX, numY);

            if (desiredRange == 0d)
            {
                FlattenTerrain(regions, desiredMax);
            }
            else
            {

                //work out current heightmap range
                double currMin = double.MaxValue;
                double currMax = double.MinValue;

                for (int x = 0; x < heightMap.Width; x++)
                {
                    for (int y = 0; y < heightMap.Height; y++)
                    {
                        double currHeight = heightMap[x, y];
                        if (currHeight < currMin)
                        {
                            currMin = currHeight;
                        }
                        else if (currHeight > currMax)
                        {
                            currMax = currHeight;
                        }
                    }
                }

                double currRange = currMax - currMin;
                double scale = desiredRange / currRange;

                m_log.InfoFormat("Current {0}, {1} = {2}", new Object[] { currMin, currMax, currRange });
                m_log.InfoFormat("Scale = {0}", scale);

                // scale the heightmap accordingly
                for (int x = 0; x < heightMap.Width; x++)
                {
                    for (int y = 0; y < heightMap.Height; y++)
                    {
                        if (ShouldTileThisPoint(x, y, numX, numY))
                        {
                            double currHeight = heightMap[x, y] - currMin;
                            regions[x / Constants.RegionSize, y / Constants.RegionSize].Heightmap[x % (int)Constants.RegionSize, y % (int)Constants.RegionSize] = desiredMin + (currHeight * scale);
                        }

                    }
                }

                UpdateTaintedRegions();
            }

        }

        private void FlattenTerrain(Scene[,] regions, double height)
        {
            for (int x = 0; x < regions.GetUpperBound(0) * Constants.RegionSize; x++)
            {
                for (int y = 0; y < regions.GetUpperBound(1) * Constants.RegionSize; y++)
                {
                        regions[x / Constants.RegionSize, y / Constants.RegionSize].Heightmap[x % (int)Constants.RegionSize, y % (int)Constants.RegionSize] = height;

                }
            }

            UpdateTaintedRegions();
        }


        private ITerrainChannel LoadFileWithLoader(FileInfo file, int numX, int numY, ITerrainLoader loader)
        {
            ITerrainChannel tiledChannel = null;

            string filename = file.FullName;

            // read the cross region heightmap using the appropriate loader
            if (IsRawFileLoader(loader))
            {
                IRawFileLoader rawLoader = (IRawFileLoader)loader;
                tiledChannel = rawLoader.LoadRawFile(filename, numX, numY);
            }
            else
            {
                tiledChannel = loader.LoadFile(filename);
            }
            return tiledChannel;
        }

        private bool IsRawFileLoader(ITerrainLoader loader)
        {
            return typeof(IRawFileLoader).IsAssignableFrom(loader.GetType());
        }

        /// <summary>
        /// Is the offset within this combined heightmap contained within one of the regions we've
        /// been asked to tile
        /// </summary>
        /// <param name="x">current x offset in heightmap</param>
        /// <param name="y">current y offset in heightmap</param>
        /// <param name="numXRegionsAskedToTile">the number of regions in the X direction we want to tile from this heightmap</param>
        /// <param name="numYRegionsAskedTotile">the number of regions in the Y direction we want to tile from this heightmap</param>
        /// <returns></returns>
        private bool ShouldTileThisPoint(int x, int y, int numXRegionsAskedToTile, int numYRegionsAskedTotile)
        {
            bool retVal = false;
            int regionXCurrentlyTiling = x / (int)Constants.RegionSize;
            int regionYCurrentlyTiling = y / (int)Constants.RegionSize;

            if (regionXCurrentlyTiling < numXRegionsAskedToTile && regionYCurrentlyTiling < numYRegionsAskedTotile)
            {
                retVal = true;
            }

            return retVal;
        }

        /// <summary>
        /// If we have modified the height maps of any of the regions in this sim, then use that regions
        /// terrain module to persist our changes and push those changes out to all connected clients
        /// 
        /// Note: currently, so that we don't need to modify the terrain module for this to work, we rely on
        /// a hack. A command is invoked on the terrain module that leaves the current heightmap unchanged
        /// but kicks the terrain module into looking for the other differences our module has introduced.
        /// 
        /// Making CheckForTerrainUpdates() public would achieve the same thing in a less hacky way
        /// </summary>
        private void UpdateTaintedRegions()
        {
            foreach (Scene scene in m_scenes)
            {
                scene.EventManager.TriggerOnPluginConsole(new String[] { "terrain", "multiply", "1.0" });
            }
        }


        /// <summary>
        /// From all of the regions registered against this sim, determine which of them lie within the boundaries
        /// described by the parameters to this method
        /// </summary>
        /// <param name="startX">X bound of the first region to include</param>
        /// <param name="startY">Y bound of the first region to include</param>
        /// <param name="numX">X extent of this subset in number of regions</param>
        /// <param name="numY">Y extent of this subset in number of regions</param>
        /// <returns>2 dimensional array of scenes affected by this tiler command</returns>
        public Scene[,] GetRegionsToTile(int startX, int startY, int numX, int numY)
        {
            Scene[,] retVal = new Scene[numX, numY];
            foreach (Scene scene in m_scenes)
            {
                RegionInfo rInfo = scene.RegionInfo;
                if (rInfo.RegionLocX >= startX &&
                    rInfo.RegionLocY >= startY &&
                    rInfo.RegionLocX < startX + numX &&
                    rInfo.RegionLocY < startY + numY)
                {
                    retVal[rInfo.RegionLocX - startX, rInfo.RegionLocY - startY] = scene;
                }
            }

            return retVal;
        }

        public bool CheckCorrectSize(int numX, int numY, int simX, int simY)
        {
            bool retVal = true;

            if (numX != simX || numY != simY)
            {
                retVal = false;
            }

            return retVal;
        }


        public void DetermineSimBounds(out int numX, out int numY, out int startX, out int startY)
        {
            startX = startY = int.MaxValue;
            int furthestX, furthestY;
            furthestX = furthestY = int.MinValue;

            foreach (Scene scene in m_scenes)
            {
                RegionInfo info = scene.RegionInfo;
                if (info.RegionLocX < startX) startX = (int)info.RegionLocX;
                if (info.RegionLocY < startY) startY = (int)info.RegionLocY;
                if (info.RegionLocX > furthestX) furthestX = (int)info.RegionLocX;
                if (info.RegionLocY > furthestY) furthestY = (int)info.RegionLocY;
            }

            numX = furthestX - startX + 1;
            numY = furthestY - startY + 1;
        }


        /// <summary>
        /// Determine if the heightmap would completely tile one or more whole regions
        /// </summary>
        /// <param name="heightmap">the heightmap we want to check the size of</param>
        /// <returns></returns>
        private bool IsHeightMapMultipleOfRegionSize(ITerrainChannel heightmap)
        {
            return heightmap.Width % Constants.RegionSize == 0 && heightmap.Height % Constants.RegionSize == 0;
        }


        /// <summary>
        /// Determine if the regions within this sim form a rectangular shape
        /// </summary>
        /// <returns>true if they do, false if they don't</returns>
        public bool IsRectangularSetOfRegionsInSim()
        {
            bool retVal = true;

            int startX, startY, numX, numY;
            DetermineSimBounds(out numX, out numY, out startX, out startY);

            for (int x = 0; x < numX; x++)
            {
                for (int y = 0; y < numY; y++)
                {
                    if (GetRegionAtLocation(startX + x, startY + y) == null)
                    {
                        retVal = false;
                        break;
                    }
                }
            }
            return retVal;
        }

        private Scene GetRegionAtLocation(int x, int y)
        {
            Scene retVal = null;

            foreach (Scene scene in m_scenes)
            {
                RegionInfo rInfo = scene.RegionInfo;
                if (rInfo.RegionLocX == x && rInfo.RegionLocY == y)
                {
                    retVal = scene;
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Register the FileLoaders that the terrain tools can use
        /// for load and save commands
        /// </summary>
        private void InstallPlugins()
        {
            // Filesystem load/save loaders
            m_loaders[".r32"] = new TilingRaw32();
            m_loaders[".f32"] = m_loaders[".r32"];
            //m_loaders[".ter"] = new Terragen(); //Terragen not currently working
            m_loaders[".raw"] = new TilingLLRaw();
            //m_loaders[".jpg"] = new JPEG(); // JPG used for map images
            //m_loaders[".jpeg"] = m_loaders[".jpg"]; 
            m_loaders[".bmp"] = new BMP();
            m_loaders[".png"] = new PNG();
            m_loaders[".gif"] = new GIF();
            m_loaders[".tif"] = new TIFF();
            m_loaders[".tiff"] = m_loaders[".tif"];

        }

        public bool IsWholeNumberOfRegions(FileInfo file)
        {
            ITerrainLoader loader = GetLoaderForFile(file);
            ITerrainChannel heightmap = loader.LoadFile(file.FullName);
            bool wholeNumOfRegions = IsHeightMapMultipleOfRegionSize(heightmap);

            return wholeNumOfRegions;
        }
    }
}
