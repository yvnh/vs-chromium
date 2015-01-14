﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using VsChromium.Commands;
using VsChromium.Core.Logging;

namespace VsChromium.Package.CommandHandler {
  [Export(typeof(IPackagePostInitializer))]
  public class PackagePriorityCommandHandlerRegistration : IPackagePostInitializer {
    private readonly IEnumerable<IPackagePriorityCommandHandler> _commandHandlers;

    [ImportingConstructor]
    public PackagePriorityCommandHandlerRegistration([ImportMany] IEnumerable<IPackagePriorityCommandHandler> commandHandlers) {
      _commandHandlers = commandHandlers;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      // Create an IOleCommandTarget wrapping all the priority command handlers
      var commandTargets = _commandHandlers
        .Select(c => new SimpleCommandTarget(
          c.CommandId,
          () => c.Execute(this, new EventArgs()),
          () => c.Supported));
      var aggregate = new AggregateCommandTarget(commandTargets);
      var oleCommandTarget = new OleCommandTarget(aggregate);

      var registerPriorityCommandTarget = package.VsRegisterPriorityCommandTarget;
      uint cookie;
      int hr = registerPriorityCommandTarget.RegisterPriorityCommandTarget(0, oleCommandTarget, out cookie);
      try {
        ErrorHandler.ThrowOnFailure(hr);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error registering priority command handler.");
        return;
      }

      package.DisposeContainer.Add(() => registerPriorityCommandTarget.UnregisterPriorityCommandTarget(cookie));
    }
  }
}