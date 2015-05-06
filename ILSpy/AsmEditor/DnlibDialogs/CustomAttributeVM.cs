﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using dnlib.DotNet;
using ICSharpCode.ILSpy.AsmEditor.ViewHelpers;
using ICSharpCode.ILSpy.TreeNodes.Filters;

namespace ICSharpCode.ILSpy.AsmEditor.DnlibDialogs
{
	sealed class CustomAttributeVM : ViewModelBase
	{
		readonly CustomAttributeOptions origOptions;

		public IDnlibTypePicker DnlibTypePicker {
			set { dnlibTypePicker = value; }
		}
		IDnlibTypePicker dnlibTypePicker;

		public ICommand ReinitializeCommand {
			get { return new RelayCommand(a => Reinitialize()); }
		}

		public ICommand PickConstructorCommand {
			get { return new RelayCommand(a => PickConstructor()); }
		}

		public ICommand AddNamedArgumentCommand {
			get { return new RelayCommand(a => AddNamedArgument(), a => AddNamedArgumentCanExecute()); }
		}

		public string TypeFullName {
			get {
				var mrCtor = Constructor as MemberRef;
				if (mrCtor != null)
					return mrCtor.GetDeclaringTypeFullName() ?? string.Empty;

				var mdCtor = Constructor as MethodDef;
				if (mdCtor != null) {
					var declType = mdCtor.DeclaringType;
					if (declType != null)
						return declType.FullName;
				}

				return string.Empty;
			}
		}

		public string FullName {
			get {
				if (IsRawData)
					return "<raw blob>";
				var sb = new StringBuilder();
				sb.Append(TypeFullName);
				sb.Append('(');
				bool first = true;
				foreach (var arg in ConstructorArguments) {
					if (!first)
						sb.Append(", ");
					first = false;
					sb.Append(arg.ToString());
				}
				foreach (var namedArg in NamedArguments) {
					if (!first)
						sb.Append(", ");
					first = false;
					sb.Append(namedArg.ToString());
				}
				sb.Append(')');
				return sb.ToString();
			}
		}

		public bool IsRawData {
			get { return isRawData; }
			set {
				if (isRawData != value) {
					isRawData = value;
					ConstructorArguments.IsEnabled = !value;
					NamedArguments.IsEnabled = !value;
					OnPropertyChanged("IsRawData");
					OnPropertyChanged("IsNotRawData");
					OnPropertyChanged("FullName");
					HasErrorUpdated();
				}
			}
		}
		bool isRawData;

		public bool IsNotRawData {
			get { return !IsRawData; }
			set { IsRawData = !value; }
		}

		public HexStringVM RawData {
			get { return rawData; }
		}
		HexStringVM rawData;

		public ICustomAttributeType Constructor {
			get { return constructor; }
			set {
				if (constructor != value) {
					constructor = value;
					OnPropertyChanged("Constructor");
					ConstructorArguments.Clear();
					CreateArguments();
					OnPropertyChanged("TypeFullName");
					OnPropertyChanged("FullName");
					HasErrorUpdated();
				}
			}
		}
		ICustomAttributeType constructor;

		public MyObservableCollection<CAArgumentVM> ConstructorArguments {
			get { return constructorArguments; }
		}
		readonly MyObservableCollection<CAArgumentVM> constructorArguments = new MyObservableCollection<CAArgumentVM>();

		public MyObservableCollection<CANamedArgumentVM> NamedArguments {
			get { return namedArguments; }
		}
		readonly MyObservableCollection<CANamedArgumentVM> namedArguments = new MyObservableCollection<CANamedArgumentVM>();

		readonly TypeSigCreatorOptions typeSigOptions;
		readonly ModuleDef module;

		public CustomAttributeVM(CustomAttributeOptions options, TypeSigCreatorOptions typeSigOptions)
		{
			this.module = typeSigOptions.Module;
			this.origOptions = options;
			this.typeSigOptions = typeSigOptions;

			this.rawData = new HexStringVM(a => HasErrorUpdated());
			ConstructorArguments.CollectionChanged += Args_CollectionChanged;
			NamedArguments.CollectionChanged += Args_CollectionChanged;

			Reinitialize();
		}

		void Args_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Hook(e);
			OnPropertyChanged("FullName");
			HasErrorUpdated();
		}

		void Hook(NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null) {
				foreach (INotifyPropertyChanged i in e.OldItems)
					i.PropertyChanged -= arg_PropertyChanged;
			}
			if (e.NewItems != null) {
				foreach (INotifyPropertyChanged i in e.NewItems)
					i.PropertyChanged += arg_PropertyChanged;
			}
		}

		void arg_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			OnPropertyChanged("FullName");
			HasErrorUpdated();
		}

		void CreateArguments()
		{
			int count = Constructor == null ? 0 : Constructor.MethodSig.GetParamCount();
			while (ConstructorArguments.Count > count)
				ConstructorArguments.RemoveAt(ConstructorArguments.Count - 1);
			while (ConstructorArguments.Count < count) {
				var type = Constructor.MethodSig.Params[ConstructorArguments.Count];
				ConstructorArguments.Add(new CAArgumentVM(CreateCAArgument(type), typeSigOptions, type));
			}
		}

		static CAArgument CreateCAArgument(TypeSig type)
		{
			var t = type.RemovePinnedAndModifiers();
			switch (t.GetElementType()) {
			case ElementType.Boolean:return new CAArgument(type, false);
			case ElementType.Char:	return new CAArgument(type, (char)0);
			case ElementType.I1:	return new CAArgument(type, (sbyte)0);
			case ElementType.U1:	return new CAArgument(type, (byte)0);
			case ElementType.I2:	return new CAArgument(type, (short)0);
			case ElementType.U2:	return new CAArgument(type, (ushort)0);
			case ElementType.I4:	return new CAArgument(type, (int)0);
			case ElementType.U4:	return new CAArgument(type, (uint)0);
			case ElementType.I8:	return new CAArgument(type, (long)0);
			case ElementType.U8:	return new CAArgument(type, (ulong)0);
			case ElementType.R4:	return new CAArgument(type, (float)0);
			case ElementType.R8:	return new CAArgument(type, (double)0);
			case ElementType.Class:
			case ElementType.ValueType:
				var tdr = ((ClassOrValueTypeSig)t).TypeDefOrRef;
				if (tdr.IsSystemType())
					return new CAArgument(type, null);
				var td = tdr.ResolveTypeDef();
				if (td == null)
					return new CAArgument(type, (int)0);
				switch (td.GetEnumUnderlyingType().RemovePinnedAndModifiers().GetElementType()) {
				case ElementType.Boolean:	return new CAArgument(type, false);
				case ElementType.Char:		return new CAArgument(type, (char)0);
				case ElementType.I1:		return new CAArgument(type, (sbyte)0);
				case ElementType.U1:		return new CAArgument(type, (byte)0);
				case ElementType.I2: 		return new CAArgument(type, (short)0);
				case ElementType.U2: 		return new CAArgument(type, (ushort)0);
				case ElementType.I4: 		return new CAArgument(type, (int)0);
				case ElementType.U4: 		return new CAArgument(type, (uint)0);
				case ElementType.I8: 		return new CAArgument(type, (long)0);
				case ElementType.U8: 		return new CAArgument(type, (ulong)0);
				case ElementType.R4: 		return new CAArgument(type, (float)0);
				case ElementType.R8: 		return new CAArgument(type, (double)0);
				}
				break;
			}
			return new CAArgument(type, null);
		}

		void PickConstructor()
		{
			if (dnlibTypePicker == null)
				throw new InvalidOperationException();
			var newCtor = dnlibTypePicker.GetDnlibType(new FlagsTreeViewNodeFilter(VisibleMembersFlags.Ctor), Constructor);
			if (newCtor != null)
				Constructor = newCtor;
		}

		void AddNamedArgument()
		{
			if (!AddNamedArgumentCanExecute())
				return;
			NamedArguments.Add(new CANamedArgumentVM(new CANamedArgument(false, module.CorLibTypes.Int32, "AttributeProperty", new CAArgument(module.CorLibTypes.Int32, 0)), typeSigOptions));
		}

		bool AddNamedArgumentCanExecute()
		{
			return !IsRawData && NamedArguments.Count < ushort.MaxValue;
		}

		void Reinitialize()
		{
			InitializeFrom(origOptions);
		}

		public CustomAttributeOptions CreateCustomAttributeOptions()
		{
			return CopyTo(new CustomAttributeOptions());
		}

		void InitializeFrom(CustomAttributeOptions options)
		{
			IsRawData = options.RawData != null;
			RawData.Value = options.RawData;
			Constructor = options.Constructor;
			ConstructorArguments.Clear();
			var sig = Constructor == null ? null : Constructor.MethodSig;
			for (int i = 0; i < options.ConstructorArguments.Count; i++) {
				TypeSig type = null;
				if (sig != null && i < sig.Params.Count)
					type = sig.Params[i];
				ConstructorArguments.Add(new CAArgumentVM(options.ConstructorArguments[i], typeSigOptions, type));
			}
			NamedArguments.Clear();
			NamedArguments.AddRange(options.NamedArguments.Select(a => new CANamedArgumentVM(a, typeSigOptions)));
			CreateArguments();
		}

		CustomAttributeOptions CopyTo(CustomAttributeOptions options)
		{
			options.Constructor = Constructor;
			options.ConstructorArguments.Clear();
			options.NamedArguments.Clear();
			if (IsRawData)
				options.RawData = RawData.Value.ToArray();
			else {
				options.RawData = null;
				int count = Constructor == null ? 0 : Constructor.MethodSig.GetParamCount();
				for (int i = 0; i < count; i++)
					options.ConstructorArguments.Add(ConstructorArguments[i].CreateCAArgument(Constructor.MethodSig.Params[i]));
				options.NamedArguments.AddRange(NamedArguments.Select(a => a.CreateCANamedArgument()));
			}
			return options;
		}

		protected override string Verify(string columnName)
		{
			return string.Empty;
		}

		public override bool HasError {
			get {
				return Constructor == null ||
					(IsRawData && rawData.HasError) ||
					(!IsRawData &&
						(ConstructorArguments.Any(a => a.HasError) ||
						NamedArguments.Any(a => a.HasError)));
			}
		}
	}
}