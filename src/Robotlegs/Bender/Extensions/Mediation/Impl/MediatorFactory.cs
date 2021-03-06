//------------------------------------------------------------------------------
//  Copyright (c) 2014-2016 the original author or authors. All Rights Reserved. 
// 
//  NOTICE: You are permitted to use, modify, and distribute this file 
//  in accordance with the terms of the license agreement accompanying it. 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Robotlegs.Bender.Extensions.Matching;
using Robotlegs.Bender.Extensions.Mediation.API;
using Robotlegs.Bender.Extensions.Mediation.DSL;
using Robotlegs.Bender.Framework.API;
using Robotlegs.Bender.Framework.Impl;

namespace Robotlegs.Bender.Extensions.Mediation.Impl
{
	public class MediatorFactory : IMediatorFactory
	{
		/*============================================================================*/
		/* Private Properties                                                         */
		/*============================================================================*/

		/// <summary>
		/// A key dictory of mediators. 
		/// _mediators[view][mapping] = mediator;
		/// </summary>
		private Dictionary<object, Dictionary<IMediatorMapping, object>> _mediators = new Dictionary<object, Dictionary<IMediatorMapping, object>>();
		
		private IInjector _injector;
		
		private IMediatorManager _manager;

		/*============================================================================*/
		/* Constructor                                                                */
		/*============================================================================*/

		public MediatorFactory (IInjector injector)
		{
			_injector = injector;
			_manager = injector.HasMapping (typeof(IMediatorManager)) 
				? injector.GetInstance (typeof(IMediatorManager)) as IMediatorManager
				: new MediatorManager ();
			_manager.ViewRemoved += RemoveMediators;
		}
		
		/*============================================================================*/
		/* Public Functions                                                           */
		/*============================================================================*/

		public object GetMediator(object item, IMediatorMapping mapping)
		{
			if (_mediators.ContainsKey(item) && _mediators[item].ContainsKey(mapping))
				return _mediators[item][mapping];
			return null;
		}

		public List<object> CreateMediators(object item, Type type, IEnumerable<IMediatorMapping> mappings)
		{
			List<object> createdMediators = new List<object>();
			object mediator;
			foreach (IMediatorMapping mapping in mappings)
			{
				mediator = GetMediator(item, mapping);

				if (mediator == null)
				{
					MapTypeForFilterBinding(mapping.Matcher, type, item);
					mediator = CreateMediator(item, mapping);
					UnmapTypeForFilterBinding(mapping.Matcher, type, item);
				}

				if (mediator != null)
				{
					createdMediators.Add (mediator);
				}
			}

			return createdMediators;
		}

		public void RemoveMediators(object item)
		{
			if (!_mediators.ContainsKey(item))
				return;

			Dictionary<IMediatorMapping, object> mediators = _mediators[item];
			foreach (IMediatorMapping mapping in mediators.Keys)
			{
				_manager.RemoveMediator(mediators[mapping], item, mapping);
			}

			_mediators.Remove(item);
		}

		public void RemoveAllMediators()
		{
			object[] mediatorKeys = new object[_mediators.Keys.Count];
			_mediators.Keys.CopyTo(mediatorKeys, 0);
			foreach (object item in mediatorKeys)
			{
				RemoveMediators(item);
			}
			_manager.ViewRemoved -= RemoveMediators;
		}

		/*============================================================================*/
		/* Private Functions                                                          */
		/*============================================================================*/
		
		private object CreateMediator(object item, IMediatorMapping mapping)
		{
			object mediator = GetMediator(item, mapping);

			if (mediator != null)
				return mediator;
			
			if (mapping.Guards.Count == 0 || Guards.Approve(_injector, mapping.Guards))
			{
				mediator = _injector.InstantiateUnmapped(mapping.MediatorType);
				if (mapping.Hooks.Count > 0)
				{
					_injector.Map(mapping.MediatorType).ToValue(mediator);
					Hooks.Apply(_injector, mapping.Hooks);
					_injector.Unmap(mapping.MediatorType);
				}
				AddMediator(mediator, item, mapping);
			}
			return mediator;
		}
		
		private void AddMediator(object mediator, object item, IMediatorMapping mapping)
		{
			if (!_mediators.ContainsKey(item))
				_mediators[item] = new Dictionary<IMediatorMapping, object>();
			_mediators[item][mapping] = mediator;
			_manager.AddMediator(mediator, item, mapping);
		}
		
		private void MapTypeForFilterBinding(ITypeFilter filter, Type type, object item)
		{
			foreach (Type requiredType in RequiredTypesFor(filter, type))
			{
				_injector.Map(requiredType).ToValue(item);
			}
		}
		
		private void UnmapTypeForFilterBinding(ITypeFilter filter, Type type, object item)
		{
			
			foreach (Type requiredType in RequiredTypesFor(filter, type))
			{
				if (_injector.SatisfiesDirectly (requiredType))
				{
					_injector.Unmap (requiredType);
				}
			}
		}
		
		private List<Type> RequiredTypesFor(ITypeFilter filter, Type type)
		{
			List<Type> requiredTypes = new List<Type>(filter.AllOfTypes);

			if (!requiredTypes.Contains (type))
			{
				requiredTypes.Add (type);
			}
			
			return requiredTypes;
		}
	}
}

