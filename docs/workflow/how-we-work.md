# How we work

A plain-language guide. No iOS or .NET knowledge assumed.

---

## The short version

You describe what you want the app to do, in your own words. I pick the right
specialist, build it, write tests, have the quality agent check it, and send
you a short update saying what was done and whether the checks passed.

You never need to choose an agent or remember a command.

---

## Who does what

Five specialists. Each owns a different part of the project, and only one can
change any given file — that is what stops two of them making conflicting
edits to the same thing.

| Specialist | Looks after | In plain terms |
| --- | --- | --- |
| **Architect** | Decisions | Decides *how* something should be built when there is a real choice, and writes down why |
| **Backend** | The server | The part that stores your data and answers the app's questions |
| **iOS** | The app | What people see and tap on the phone |
| **QA** | Quality | Decides what "working" means, runs the checks, reviews the result |
| **DevOps** | The machinery | Builds, tests and deployment pipelines — the plumbing that makes everything repeatable |

QA deliberately **cannot edit any code**. It can only read and report. A
reviewer able to quietly fix what it finds is a reviewer that hides problems.

---

## What happens when you ask for something

1. **Understand** — if the request is big or there is a real choice to make,
   the Architect works out the approach first and I show it to you before any
   code is written. Small, obvious changes skip this.
2. **Build** — the Backend or iOS specialist implements it. If a change needs
   both, they work in a set order so they never collide.
3. **Test** — whoever built it writes tests that fail without the change.
4. **Check** — QA runs the full set of checks and reviews the result.
5. **Report** — you get a short update.

### Your update, every time

> **What I did** — one or two sentences, no jargon.
> **Checks** — which ran, and whether they passed, failed, or could not run.
> **Anything you should know** — a decision made, a risk taken, or a question.

"Could not run" is always reported as its own outcome. It is never folded into
"passed". If a check was skipped you will be told, because a skipped check
proves nothing.

---

## What you can say

Plain descriptions work. You do not need to name files or specialists.

> "Let people sign in with an email and password."

> "The users list should show the newest person first."

> "Add a way to delete an account."

> "The app feels slow when the list is long."

If you want something specific, say so in ordinary language — "make sure this
works offline", "this needs to be fast", "don't let anyone see someone else's
data". Those become test cases.

---

## Commands you might use yourself

You do not have to, but these are the useful ones:

| Command | What it does |
| --- | --- |
| `make up` | Starts the server and database on your machine |
| `make down` | Stops them |
| `make test` | Runs every test |
| `make doctor` | Says which tools are installed and what any missing one blocks |
| `make ios-project` | Rebuilds the Xcode project file, then open `ios/Atlas.xcodeproj` |

Run them from the `atlas` folder in Terminal.

---

## Where things are written down

Nothing important lives only in a chat.

- **Decisions** — `docs/adr/`. One file per decision, with what it costs.
- **Risks and open questions** — `docs/workflow/open-items.md`. Anything
  unresolved, including questions waiting on you.
- **How the system fits together** — `docs/architecture.md`.

---

## Two honest limits right now

**The deployment pipeline has never run.** GitHub is blocking it because of a
billing lock on the account. Everything is tested on this machine instead,
which is thorough but is not the same as proving it works on a clean one. Once
the lock clears, the pipeline needs one real run before anyone trusts it.

**Nothing is deployed anywhere yet.** The app and server run on your machine.
Putting them on real infrastructure needs decisions about hosting that have
not been made — see question Q3 in the open items.
