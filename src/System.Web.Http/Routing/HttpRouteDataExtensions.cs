﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Web.Http.Controllers;

namespace System.Web.Http.Routing
{
    public static class HttpRouteDataExtensions
    {
        /// <summary>
        /// Remove all optional parameters that do not have a value from the route data. 
        /// </summary>
        /// <param name="routeData">route data, to be mutated in-place.</param>
        public static void RemoveOptionalRoutingParameters(this IHttpRouteData routeData)
        {
            RemoveOptionalRoutingParameters(routeData.Values);

            var subRouteData = routeData.GetSubRoutes();
            if (subRouteData != null)
            {
                foreach (IHttpRouteData sub in subRouteData)
                {
                    RemoveOptionalRoutingParameters(sub);
                }
            }
        }

        private static void RemoveOptionalRoutingParameters(IDictionary<string, object> routeValueDictionary)
        {
            Contract.Assert(routeValueDictionary != null);

            // Get all keys for which the corresponding value is 'Optional'.
            // Having a separate array is necessary so that we don't manipulate the dictionary while enumerating.
            // This is on a hot-path and linq expressions are showing up on the profile, so do array manipulation.
            int max = routeValueDictionary.Count;
            int i = 0;
            string[] matching = new string[max];
            foreach (KeyValuePair<string, object> kv in routeValueDictionary)
            {
                if (kv.Value == RouteParameter.Optional)
                {
                    matching[i] = kv.Key;
                    i++;
                }
            }
            for (int j = 0; j < i; j++)
            {
                string key = matching[j];
                routeValueDictionary.Remove(key);
            }
        }

        // If routeData is from an attribute route, get the controller that can handle it. 
        // Else return null.
        internal static HttpControllerDescriptor GetDirectRouteController(this IHttpRouteData routeData)
        {
            CandidateAction[] candidates = routeData.GetDirectRouteCandidates();
            if (candidates != null)
            {
                // Set the controller descriptor for the first action descriptor
                Contract.Assert(candidates.Length > 0);
                Contract.Assert(candidates[0].ActionDescriptor != null);
                HttpControllerDescriptor controllerDescriptor = candidates[0].ActionDescriptor.ControllerDescriptor;

                // Check that all other candidate action descriptors share the same controller descriptor
                for (int i = 1; i < candidates.Length; i++)
                {
                    if (candidates[i].ActionDescriptor.ControllerDescriptor != controllerDescriptor)
                    {
                        return null;
                    }
                }                

                return controllerDescriptor;
            }

            return null;
        }

        /// <summary>
        /// If a route is really a union of other routes, return the set of sub routes. 
        /// </summary>
        /// <param name="routeData">a union route data</param>
        /// <returns>set of sub soutes contained within this route</returns>
        public static IEnumerable<IHttpRouteData> GetSubRoutes(this IHttpRouteData routeData)
        {
            IHttpRouteData[] subRoutes = null;
            if (routeData.Values.TryGetValue(RouteCollectionRoute.SubRouteDataKey, out subRoutes))
            {
                return subRoutes;
            }
            return null;
        }

        // If routeData is from an attribute route, get the action descriptors, order and precedence that it may match
        // to. Caller still needs to run action selection to pick the specific action.
        // Else return null.
        internal static CandidateAction[] GetDirectRouteCandidates(this IHttpRouteData routeData)
        {
            Contract.Assert(routeData != null);
            IEnumerable<IHttpRouteData> subRoutes = routeData.GetSubRoutes();
            if (subRoutes == null)
            {
                // Possible this is being called on a subroute. This can happen after ElevateRouteData. Just chain. 
                return routeData.Route.GetDirectRouteCandidates();
            }

            var list = new List<CandidateAction>();

            foreach (IHttpRouteData subData in subRoutes)
            {
                CandidateAction[] candidates = subData.Route.GetDirectRouteCandidates();
                if (candidates != null)
                {
                    list.AddRange(candidates);
                }
            }
            return list.ToArray();
        }
    }
}