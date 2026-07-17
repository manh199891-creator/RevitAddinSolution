using System;
using Autodesk.Revit.DB;

namespace DoorClearanceBox.Core
{
    /// <summary>
    /// Layer 3 — TransactionWrapper
    ///
    /// Provides a thin, consistent wrapper around Revit's Transaction API with:
    ///   • Automatic rollback on any exception
    ///   • Named transactions for the undo stack
    ///   • Optional SubTransaction variant for use inside IUpdater.Execute()
    /// </summary>
    internal static class TransactionWrapper
    {
        /// <summary>
        /// Executes <paramref name="action"/> inside a named transaction.
        /// The transaction is committed on success and rolled back on any exception.
        /// The exception is re-thrown after rollback so callers can handle it.
        /// </summary>
        /// <param name="doc">Target document.</param>
        /// <param name="name">Name shown in the Revit undo stack.</param>
        /// <param name="action">Work to perform; receives the active transaction.</param>
        public static void Execute(Document doc, string name, Action<Transaction> action)
        {
            using (var tx = new Transaction(doc, name))
            {
                tx.Start();
                try
                {
                    action(tx);
                    tx.Commit();
                }
                catch
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Executes <paramref name="action"/> inside a named transaction,
        /// returning a value from <typeparamref name="T"/>.
        /// Rolls back and re-throws on failure.
        /// </summary>
        public static T Execute<T>(Document doc, string name, Func<Transaction, T> action)
        {
            using (var tx = new Transaction(doc, name))
            {
                tx.Start();
                try
                {
                    T result = action(tx);
                    tx.Commit();
                    return result;
                }
                catch
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Executes a lightweight <see cref="SubTransaction"/>.
        /// Use this inside <see cref="Autodesk.Revit.DB.IUpdater.Execute"/> where
        /// an outer transaction is already active and a new Transaction cannot be started.
        /// Rolls back and re-throws on failure.
        /// </summary>
        public static void ExecuteSub(Document doc, Action action)
        {
            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    action();
                    sub.Commit();
                }
                catch
                {
                    if (sub.GetStatus() == TransactionStatus.Started)
                        sub.RollBack();
                    throw;
                }
            }
        }
    }
}
