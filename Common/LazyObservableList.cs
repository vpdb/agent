using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VpdbAgent.Common
{
	public class LazyObservableList<T> : List<T>, INotifyCollectionChanged, INotifyPropertyChanged
	{

		//------------------------------------------------------
		//
		//  Public Methods
		//
		//------------------------------------------------------

		#region Public Methods

		public void NotifyRepopulated()
		{
			OnPropertyChanged(CountString);
			OnPropertyChanged(IndexerName);
			OnCollectionReset();
		}

		public void NotifyChanged()
		{
			OnPropertyChanged(CountString);
			OnPropertyChanged(IndexerName);
		}

		#endregion Public Events


		//------------------------------------------------------
		//
		//  Public Events
		//
		//------------------------------------------------------

		#region Public Events

		//------------------------------------------------------
		#region INotifyPropertyChanged implementation
		/// <summary>
		/// PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
		/// </summary>
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
		{
			add
			{
				PropertyChanged += value;
			}
			remove
			{
				PropertyChanged -= value;
			}
		}
		#endregion INotifyPropertyChanged implementation


		//------------------------------------------------------
		/// <summary>
		/// Occurs when the collection changes, either by adding or removing an item.
		/// </summary>
		/// <remarks>
		/// see <seealso cref="INotifyCollectionChanged"/>
		/// </remarks>
#if !FEATURE_NETCORE
		[field: NonSerializedAttribute()]
#endif
		public virtual event NotifyCollectionChangedEventHandler CollectionChanged;

		#endregion Public Events


		//------------------------------------------------------
		//
		//  Private Methods
		//
		//------------------------------------------------------

		#region Private Methods
		/// <summary>
		/// Helper to raise a PropertyChanged event  />).
		/// </summary>
		private void OnPropertyChanged(string propertyName)
		{
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Helper to raise CollectionChanged event to any listeners
		/// </summary>
		private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));
		}

		/// <summary>
		/// Helper to raise CollectionChanged event to any listeners
		/// </summary>
		private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index, int oldIndex)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index, oldIndex));
		}

		/// <summary>
		/// Helper to raise CollectionChanged event to any listeners
		/// </summary>
		private void OnCollectionChanged(NotifyCollectionChangedAction action, object oldItem, object newItem, int index)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));
		}

		/// <summary>
		/// Helper to raise CollectionChanged event with action == Reset to any listeners
		/// </summary>
		private void OnCollectionReset()
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
		#endregion Private Methods


		//------------------------------------------------------
		//
		//  Protected Methods
		//
		//------------------------------------------------------

		#region Protected Methods

		/// <summary>
		/// Raises a PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
		/// </summary>
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, e);
			}
		}

		/// <summary>
		/// PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
		/// </summary>
		protected virtual event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raise CollectionChanged event to any listeners.
		/// Properties/methods modifying this ObservableCollection will raise
		/// a collection changed event through this virtual method.
		/// </summary>
		/// <remarks>
		/// When overriding this method, either call its base implementation
		/// or call <see cref="BlockReentrancy"/> to guard against reentrant collection changes.
		/// </remarks>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if (CollectionChanged != null) {
				CollectionChanged(this, e);
			}
		}

		#endregion Protected Methods

		//------------------------------------------------------
		//
		//  Private Fields
		//
		//------------------------------------------------------

		#region Private Fields

		private const string CountString = "Count";

		// This must agree with Binding.IndexerName.  It is declared separately
		// here so as to avoid a dependency on PresentationFramework.dll.
		private const string IndexerName = "Item[]";

		#endregion Private Fields
	}
}
