using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Pages
{
	public interface IReactiveView : IViewFor
	{
	}

	public interface IReactiveView<T> : IReactiveView, IViewFor<T> where T : class
	{
	}
}
