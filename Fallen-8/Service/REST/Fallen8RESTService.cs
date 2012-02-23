// 
//  Fallen8RESTService.cs
//  
//  Author:
//       Henning Rauch <Henning@RauchEntwicklung.biz>
//  
//  Copyright (c) 2012 Henning Rauch
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, version 3 of the License.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Linq;
using System.Collections.Generic;
using Fallen8.API.Model;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Fallen8.API.Plugin;
using Fallen8.API.Index;
using Fallen8.API.Algorithms.Path;

namespace Fallen8.API.Service.REST
{
	/// <summary>
	/// Fallen-8 REST service.
	/// </summary>
	public sealed class Fallen8RESTService : IFallen8RESTService, IDisposable
	{
		#region Data

        /// <summary>
        ///   The internal Fallen-8 instance
        /// </summary>
        private readonly Fallen8 _fallen8;
		
		#endregion
		
		#region Constructor
		
		/// <summary>
		/// Initializes a new instance of the Fallen8RESTService class.
		/// </summary>
		/// <param name='fallen8'>
		/// Fallen-8.
		/// </param>
		public Fallen8RESTService(Fallen8 fallen8)
		{
			_fallen8 = fallen8;
		}
		
		#endregion
		
		#region IDisposable Members

        public void Dispose()
        {
			//do nothing atm
        }

        #endregion

		#region IFallen8RESTService implementation
		
		public int AddVertex (VertexSpecification definition)
		{
            #region initial checks

            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            #endregion
			
            return _fallen8.CreateVertex(definition.CreationDate, GenerateProperties(definition.Properties)).Id;
		}

		public int AddEdge (EdgeSpecification definition)
		{
			#region initial checks

            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }
		
            #endregion

            return _fallen8.CreateEdge(definition.SourceVertex, definition.EdgePropertyId, definition.TargetVertex, definition.CreationDate, GenerateProperties(definition.Properties)).Id;
		}

		public Dictionary<ushort, string> GetAllVertexProperties (string vertexIdentifier)
		{
			return GetGraphElementProperties (vertexIdentifier);
		}

		public Dictionary<ushort, string> GetAllEdgeProperties (string edgeIdentifier)
		{
			return GetGraphElementProperties (edgeIdentifier);
		}

		public List<ushort> GetAllAvailableOutEdgesOnVertex (string vertexIdentifier)
		{
			VertexModel vertex;
			if (_fallen8.TryGetVertex(out vertex, Convert.ToInt32(vertexIdentifier))) 
			{
				return vertex.GetOutgoingEdgeIds();
			}
			return null;
		}

		public List<ushort> GetAllAvailableIncEdgesOnVertex (string vertexIdentifier)
		{
			VertexModel vertex;
			if (_fallen8.TryGetVertex(out vertex, Convert.ToInt32(vertexIdentifier))) 
			{
				return vertex.GetIncomingEdgeIds();
			}
			return null;
		}

		public List<int> GetOutgoingEdges (string vertexIdentifier, string edgePropertyIdentifier)
		{
			VertexModel vertex;
			if (_fallen8.TryGetVertex(out vertex, Convert.ToInt32(vertexIdentifier))) 
			{
				ReadOnlyCollection<EdgeModel> edges;
				if (vertex.TryGetOutEdge(out edges, Convert.ToInt32(edgePropertyIdentifier))) 
				{
					return edges.Select(_ => _.Id).ToList();
				}
			}
			return null;
		}

		public List<int> GetIncomingEdges (string vertexIdentifier, string edgePropertyIdentifier)
		{
			VertexModel vertex;
			if (_fallen8.TryGetVertex(out vertex, Convert.ToInt32(vertexIdentifier))) 
			{
				ReadOnlyCollection<EdgeModel> edges;
				if (vertex.TryGetInEdges(out edges, Convert.ToInt32(edgePropertyIdentifier))) 
				{
					return edges.Select(_ => _.Id).ToList();
				}
			}
			return null;
		}

		public void Trim ()
		{
			_fallen8.Trim();
		}

		public Fallen8Status Status ()
		{
			var currentProcess = Process.GetCurrentProcess();
            var totalBytesOfMemoryUsed = currentProcess.WorkingSet64;
			
			PerformanceCounter freeMem = new PerformanceCounter("Memory", "Available Bytes");
			var freeBytesOfMemory = Convert.ToInt64(freeMem.NextValue());
			
			var vertexCount = _fallen8.GetVertices().Count;
			var edgeCount = _fallen8.GetVertices().Count;
			
			IEnumerable<String> availableIndices;
			Fallen8PluginFactory.TryGetAvailablePlugins<IIndex>(out availableIndices);
			
			IEnumerable<String> availablePathAlgos;
			Fallen8PluginFactory.TryGetAvailablePlugins<IShortestPathAlgorithm>(out availablePathAlgos);
			
			IEnumerable<String> availableServices;
			Fallen8PluginFactory.TryGetAvailablePlugins<IFallen8Service>(out availableServices);
			
			return new Fallen8Status
			{
				AvailableIndexPlugins = new List<String>(availableIndices),
				AvailablePathPlugins = new List<String>(availablePathAlgos),
				AvailableServicePlugins = new List<String>(availableServices),
				EdgeCount = edgeCount,
				VertexCount = vertexCount,
				UsedMemory = totalBytesOfMemoryUsed,
				FreeMemory = freeBytesOfMemory
			};
		}
		#endregion
		
		#region private helper
		
		/// <summary>
		/// Generates the properties.
		/// </summary>
		/// <returns>
		/// The properties.
		/// </returns>
		/// <param name='propertySpecification'>
		/// Property specification.
		/// </param>
		private static PropertyContainer[] GenerateProperties (Dictionary<UInt16, PropertySpecification> propertySpecification)
		{
			PropertyContainer[] properties = null;
			
            if (propertySpecification != null)
            {
                var propCounter = 0;
				properties = new PropertyContainer[propertySpecification.Count];
				
                foreach (var aPropertyDefinition in propertySpecification)
                {
                    properties[propCounter] = new PropertyContainer 
					{ 
						PropertyId = aPropertyDefinition.Key, 
						Value = Convert.ChangeType(aPropertyDefinition.Value.Property, Type.GetType(aPropertyDefinition.Value.TypeName, true, true)) 
					};
                    propCounter++;
				}
            }
		
			return properties;
		}
		
		/// <summary>
		/// Gets the graph element properties.
		/// </summary>
		/// <returns>
		/// The graph element properties.
		/// </returns>
		/// <param name='vertexIdentifier'>
		/// Vertex identifier.
		/// </param>
		private Dictionary<ushort, string> GetGraphElementProperties (string vertexIdentifier)
		{
			AGraphElement vertex;
			if (_fallen8.TryGetGraphElement(out vertex, Convert.ToInt32(vertexIdentifier))) 
			{
				var result = new Dictionary<ushort, String>();
				var properties = vertex.GetAllProperties();
				for (int i = 0; i < properties.Count; i++) 
				{
					var propertyContainer = properties[i];
					result.Add(propertyContainer.PropertyId, propertyContainer.Value.ToString());
				}
				
				return result;
			}
			
			return null;
		}
		
		#endregion
	}
}

