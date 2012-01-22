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
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;

namespace TerrainTools
{
    public class TerrainToolsModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Controller for interpreting Terrain commands
        private  TerrainToolsController m_controller = null;
        
        // model
        private TerrainTools m_model = new TerrainTools();

        //we don't want the console commands to fire against each region, 
        //so only register this set of commands once against the default region
        //this flag used to determine if we are registered
        private bool m_registered = false;

        #region IRegionModule Members


        /// <summary>
        /// Initialise this shared module
        /// This  consists of keeping track of all the scenes in the sim that is starting up
        /// However, because we are sharing this module across all those regions
        /// we only want to register it with the first one rather than all of them
        /// other wise we will apply the same tiling commands multiple times.
        /// </summary>
        /// <param name="scene">this region is getting initialised</param>
        /// <param name="source">nini config, we are not using this</param>
        public void Initialise(Scene scene, IConfigSource source)
        {
            // add all the scenes to the list of known regions
            m_model.AddRegion(scene);

            //only register the command interface against the default region
            if (!m_registered)
            {
                m_registered = true;
            }

        }

        /// <summary>
        /// everything is loaded, perform post load configuration
        /// </summary>
        public void PostInitialise()
        {
            m_controller = new TerrainToolsController( m_model );
        }


        /// <summary>
        /// Nothing to do on close
        /// </summary>
        public void Close()
        {
            
        }

        /// <summary>
        /// Name of this shared module is it's class name
        /// </summary>
        public string Name
        {
            get { return this.GetType().Name; }
        }

        /// <summary>
        /// Tell the runtime that we are a shared module
        /// so a single instance of us is used by all the regions hosted in this Simulator
        /// </summary>
        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

    }
}
