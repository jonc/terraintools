﻿/*
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
using System.Text;
using System.IO;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Terrain.FileLoaders;

namespace TerrainTools.FileLoaders
{
    /// <summary>
    /// Extend the OpenSim LLRAW fileloader to make it a raw file loader
    /// 
    /// </summary>
    public class TilingLLRaw: LLRAW, IRawFileLoader
    {
        public TilingLLRaw()
            : base()
        {
        }


        #region IRawFileLoader Members

        /// <summary>
        /// Load the LLRaw file specified into a hegihtmap of a given shape
        /// </summary>
        /// <param name="filename">the llraw file to load</param>
        /// <param name="width">the width of the heightmap to generate</param>
        /// <param name="height">the height of the heightmap to generate</param>
        /// <returns></returns>
        public ITerrainChannel LoadRawFile(string filename, int width, int height)
        {
            TerrainChannel retval = new TerrainChannel((int)(width*Constants.RegionSize), (int)(height*Constants.RegionSize));

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int y;
            for (y = 0; y < retval.Height; y++)
            {
                int x;
                for (x = 0; x < retval.Width; x++)
                {
                    retval[x, y] = bs.ReadByte() * (bs.ReadByte() / 128.0);
                    bs.ReadBytes(11); // Advance the stream to next bytes.
                }
            }

            bs.Close();

            return retval;
        }

        #endregion
    }
}
