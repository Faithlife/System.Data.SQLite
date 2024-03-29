# Inactive Project

[![Project Status: Inactive – The project has reached a stable, usable state but is no longer being actively developed; support/maintenance will be provided as time allows.](https://www.repostatus.org/badges/latest/inactive.svg)](https://www.repostatus.org/#inactive) 

This project is inactive. We recommend using one of the following supported alternatives:

* System.Data.SQLite - [Home Page](https://system.data.sqlite.org/), [NuGet](https://www.nuget.org/packages/System.Data.SQLite.Core).
* Microsoft.Data.Sqlite - [Home Page](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/), [NuGet](https://www.nuget.org/packages/Microsoft.Data.Sqlite).

(Note to Faithlife developers: This package should continue to be used in Faithlife applications; the above notice is for external developers who find this project.)

# A lightweight cross-platform replacement for System.Data.SQLite

This is an independent implementation of the core of ADO.NET: `IDbConnection`, `IDbCommand`, `IDbDataReader`, `IDbTransaction` (plus a few helpers) — enough types to let you create and query SQLite databases from managed code, including support for libraries such as [Dapper](https://dapperlib.github.io/Dapper/).

It supports the following platforms: .NET Framework 4.7.2., .NET Standard 2.0, Xamarin.iOS, MonoAndroid.

## Build Status

[![Build status](https://img.shields.io/appveyor/ci/Faithlife/system-data-sqlite.svg)](https://ci.appveyor.com/project/Faithlife/system-data-sqlite) [![NuGet](https://img.shields.io/nuget/v/Faithlife.System.Data.SQLite.svg)](https://www.nuget.org/packages/Faithlife.System.Data.SQLite/)

## Why?

1. Lightweight
 * Only the core of ADO.NET is implemented, not EF or Designer types.
 * The official System.Data.SQLite is over 300KB; this library is under 50KB.
2. High performance
 * This library assumes that the caller will use `IDisposable` properly, so it avoids adding finalizers to clean up incorrect usage.
 * No static constructors (e.g., `SQLiteFunction`) that reflect over all loaded assemblies.
3. Cross-platform support
 * Works on desktop and mobile devices
4. Tested
 * This implementation has been shipping in [Logos 6](https://www.logos.com/install) and installed on tens of thousands of client machines. The developers track and fix crashes reported via [Raygun](https://raygun.io/).

## Enhancements

* `DbDataReader.ReadAsync(CancellationToken)` is overridden to support cancellation from another thread (via [`sqlite3_interrupt`](https://www.sqlite.org/c3ref/interrupt.html)).
* Added [`SQLiteConnection.StatementCompleted`](https://github.com/Faithlife/System.Data.SQLite/search?q=StatementCompleted) to return the results of [`sqlite3_profile`](https://www.sqlite.org/c3ref/profile.html).

## Compatibility

This library is generally compatible with the official System.Data.SQLite API, but a few changes were made where necessary:
* [SQLiteConnectionStringBuilder](https://github.com/Faithlife/System.Data.SQLite/blob/master/src/System.Data.SQLite/SQLiteConnectionStringBuilder.cs) does not support all the official connection string properties.
* [SQLiteDataReader](https://github.com/Faithlife/System.Data.SQLite/blob/master/src/System.Data.SQLite/SQLiteDataReader.cs) performs fewer implicit conversions.
* Not all SQL type aliases (`text`, `int`, `blob`, etc.) are supported.

This wrapper is managed-only; you still need a copy of the native SQLite library. A recent copy is provided in the `lib` folder (for the unit tests).

## Async

This library implements all the `*Async` methods of `DbCommand`, etc. However, because SQLite itself performs
synchronous I/O (and it would be extremely difficult to make it truly async), they don't actually have an async
implementation, but will run synchronously. (Using `Task.Run` in the implementation is [a bad idea](https://blog.stephencleary.com/2013/11/taskrun-etiquette-examples-dont-use.html);
see also [here](https://devblogs.microsoft.com/pfxteam/should-i-expose-asynchronous-wrappers-for-synchronous-methods/) and [here](https://blog.stephencleary.com/2013/11/taskrun-etiquette-examples-using.html).)
If you need to perform database work off the UI thread, use `Task.Run` in the UI code to execute a series of
SQLite calls on a background thread.

The `*Async` methods *do* support cancellation, though. If you pass in a `CancellationToken`, the methods will
still run synchronously, but you can interrupt them (even if they're in a long-running loop in SQLite's native code)
by cancelling the cancellation token from another thread. (For example, you can cancel DB work that's happening
on a threadpool thread when a user clicks a "Cancel" button in the UI.)
