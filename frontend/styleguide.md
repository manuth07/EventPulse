# EventPulse UI Style Guide

This document defines the mandatory visual and frontend design standards for **EventPulse**.

Every new frontend feature, page, component, modal, form, dashboard, card, navigation element, booking interface, authentication screen, admin screen, organizer screen, and customer-facing screen must follow this guide.

The purpose is to maintain a **single consistent product identity** across the entire application.

Do not redesign the visual language per feature.

---

# 1. Product Design Direction

EventPulse should feel like a modern, polished event-ticketing product.

The visual direction is:

> **Apple-inspired restraint + EventPulse orange identity + modern ticket marketplace**

The interface should feel:

* clean
* premium
* calm
* spacious
* modern
* professional
* trustworthy
* lightweight
* intentional
* easy to scan
* suitable for a real production product

The interface should NOT feel:

* AI generated
* like a generic SaaS template
* like a Bootstrap demo
* overly playful
* overly colorful
* like a crypto dashboard
* like a gaming interface
* like an admin template purchased online
* visually noisy

---

# 2. Current Visual Baseline

The existing EventPulse homepage is the visual baseline.

Its key characteristics should be preserved:

```text
White / off-white surfaces
        +
Strong whitespace
        +
Dark typography
        +
Orange accent
        +
Thin neutral borders
        +
Subtle shadows
        +
Large clean headings
        +
Simple event cards
        +
Minimal navigation
```

New features must feel like they were designed as part of the same product.

Do not introduce a completely new visual language on another page.

---

# 3. Core Design Principle

Use the following rule:

> **If an element does not improve hierarchy, clarity, navigation, feedback, or usability, do not add it.**

Avoid decorative UI for decoration's sake.

Prefer:

```text
Whitespace
Typography
Alignment
Spacing
Hierarchy
```

over:

```text
Gradients
Glows
Decorative blobs
Random illustrations
Large icon collections
Excessive badges
```

---

# 4. Primary Color Palette

## Primary Orange

```text
#FF5B00
```

Use for:

* primary CTA buttons
* selected states
* active navigation
* important links
* EventPulse wordmark accent
* active filters
* progress highlights
* small important indicators

Orange should be visually important because it is the EventPulse brand color.

However:

> Do not make every element orange.

Orange should remain an accent.

---

## Orange Hover

```text
#E05000
```

Use for primary button hover states.

---

## Orange Pressed

```text
#C44300
```

Use for active/pressed states.

---

## Soft Orange Background

```text
#FFF0E6
```

Use sparingly for:

* selected cards
* event visual headers
* active filter backgrounds
* subtle information blocks
* highlighted states
* small badges

---

# 5. Neutral Palette

## Main Page Background

```text
#F5F5F7
```

Use for large application/background sections.

---

## Primary Surface

```text
#FFFFFF
```

Use for:

* cards
* modals
* navigation
* forms
* summary panels
* content sections

---

## Primary Text

```text
#1D1D1F
```

Use for:

* headings
* important values
* event names
* navigation
* main labels

Never use pure black unless absolutely necessary.

---

## Secondary Text

```text
#86868B
```

Use for:

* secondary descriptions
* dates
* venues
* helper text
* metadata
* subtitles

---

## Borders

```text
#E5E5EA
```

Use for:

* cards
* input outlines
* separators
* tables
* navigation borders
* modal borders

Borders should remain subtle.

---

# 6. Functional Colors

Use functional colors only when they communicate actual state.

## Success

```text
#34C759
```

Examples:

* payment succeeded
* event approved
* booking confirmed
* valid operation

---

## Danger

```text
#FF3B30
```

Examples:

* destructive action
* cancellation
* deletion
* failed payment
* validation error

---

## Warning

Use a restrained amber/yellow only when required.

Do not make warning sections visually overwhelming.

---

# 7. Orange Usage Rule

Orange must remain a controlled brand accent.

Recommended visual distribution:

```text
~70–80% white / off-white
~15–20% neutral/dark text and borders
~5–10% orange accent
```

Do not create screens where every card, heading, icon, badge and border is orange.

Too much orange reduces its ability to communicate priority.

---

# 8. Typography

Use the system Apple-style font stack.

```css
font-family:
  -apple-system,
  BlinkMacSystemFont,
  "SF Pro Display",
  "SF Pro Text",
  "Helvetica Neue",
  Arial,
  sans-serif;
```

Do not introduce decorative fonts.

Do not use multiple unrelated font families.

---

# 9. Typography Scale

## Hero Display

Desktop:

```text
Font size: 44–52px
Line height: 1.05–1.1
Weight: 700
Letter spacing: -0.015em
```

Use only for major landing-page hero text.

Example:

```text
Discover experiences worth remembering.
```

---

## H1

```text
32px
line-height: 1.2
font-weight: 700
letter-spacing: -0.01em
```

---

## H2

```text
24px
line-height: 1.3
font-weight: 600
letter-spacing: -0.005em
```

---

## H3

```text
20px
line-height: 1.4
font-weight: 600
```

---

## H4

```text
16px
line-height: 1.4
font-weight: 600
```

---

## Body Large

```text
16px
line-height: 1.5
font-weight: 400
```

---

## Body Regular

```text
14px
line-height: 1.5
font-weight: 400
```

---

## Caption

```text
12px
line-height: 1.4
font-weight: 500
```

---

# 10. Heading Style

Headings should:

* be concise
* use dark charcoal text
* have generous whitespace
* avoid unnecessary decoration
* avoid gradients inside text
* avoid orange unless there is a strong reason

Primary headings should generally remain:

```text
#1D1D1F
```

Orange can be used for small highlights or parts of branding.

---

# 11. Spacing System

Use a consistent spacing scale.

Preferred spacing values:

```text
4px
8px
12px
16px
20px
24px
32px
40px
48px
64px
80px
96px
```

Avoid arbitrary values such as:

```text
17px
29px
43px
```

unless required by an existing component.

---

# 12. Page Width

Content should not stretch across the full monitor unnecessarily.

Recommended maximum content width:

```text
1200px – 1320px
```

Center content using:

```css
margin: 0 auto;
```

Use generous horizontal padding.

Desktop:

```text
32–48px
```

Tablet:

```text
24px
```

Mobile:

```text
16–20px
```

---

# 13. Vertical Rhythm

Sections should have generous spacing.

Typical major section spacing:

```text
64px – 96px
```

Desktop pages should not feel cramped.

Form screens may use slightly tighter spacing.

---

# 14. Border Radius

Use consistent rounding.

## Main cards

```text
16px
```

## Secondary containers

```text
12px
```

## Buttons

```text
10–12px
```

## Inputs

```text
10–12px
```

## Small badges

```text
8px
```

## Pills

```text
9999px
```

Do not make every component pill-shaped.

---

# 15. Shadows

Use shadows very subtly.

Default:

```css
box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
```

Elevated card if required:

```css
box-shadow: 0 6px 20px rgba(0, 0, 0, 0.06);
```

Do NOT use:

* large dark shadows
* neon shadows
* orange glowing cards
* heavy floating effects

Borders and whitespace should create most of the visual separation.

---

# 16. Focus State

Interactive controls should have a clear focus state.

Use:

```css
box-shadow: 0 0 0 3px rgba(255, 91, 0, 0.20);
```

Focus should remain visible for keyboard users.

---

# 17. Header / Navbar

Use a minimal navigation bar.

Visual direction:

```text
EventPulse                                  Location     Sign In
```

The header should have:

* white background
* thin bottom border
* restrained height
* clear EventPulse branding
* minimal navigation controls

Recommended desktop height:

```text
72–88px
```

Do not overcrowd the header.

---

# 18. EventPulse Wordmark

Use:

```text
EventPulse
```

with:

```text
Event = #1D1D1F
Pulse = #FF5B00
```

Keep the wordmark simple.

Do not generate complicated AI logos.

Do not add random lightning bolts, gradients or futuristic symbols.

---

# 19. Buttons

## Primary Button

Background:

```text
#FF5B00
```

Text:

```text
#FFFFFF
```

Hover:

```text
#E05000
```

Pressed:

```text
#C44300
```

Typical style:

```text
Height: 44–52px
Padding: 0 20–24px
Radius: 10–12px
Weight: 600
```

---

## Secondary Button

Background:

```text
#FFFFFF
```

Text:

```text
#1D1D1F
```

Border:

```text
#E5E5EA
```

Hover background:

```text
#F5F5F7
```

---

## Destructive Button

Use red only for actual destructive actions.

Example:

```text
Delete Event
Cancel Booking
```

Do not use red as normal decoration.

---

# 20. Button Rules

Buttons must contain concise action-oriented labels.

Good:

```text
View Details
Create Event
Save Changes
Proceed to Checkout
Approve Event
Sign In
```

Avoid:

```text
Click Here
Continue Now!!!
Let's Go 🚀
```

Do not put emojis inside buttons.

---

# 21. Iconography

This rule is mandatory.

## DO NOT USE COLORFUL EMOJIS AS UI ICONS

Never use emojis such as:

```text
📍
🎫
🔥
🚀
🎉
🔍
❤️
⭐
✅
❌
💳
👤
⚙️
📅
```

as interface icons.

They make the application visually inconsistent and can make the interface look AI generated.

---

# 22. Approved Icon Style

Use professional monochrome interface icons.

Preferred appearance:

```text
Stroke icons
Black / dark charcoal
Grey for secondary icons
Orange only for active/highlighted state
```

Recommended icon colors:

Primary:

```text
#1D1D1F
```

Secondary:

```text
#86868B
```

Active:

```text
#FF5B00
```

Danger:

```text
#FF3B30
```

---

# 23. Icon Libraries

Use the project's existing icon library if one already exists.

Otherwise prefer one professional icon system such as:

```text
Lucide
Bootstrap Icons
Heroicons
```

Do NOT mix multiple icon libraries unnecessarily.

Do NOT introduce an icon dependency solely for one icon if a clean SVG already exists.

---

# 24. Icon Style Consistency

All icons on the same interface should share similar:

* stroke width
* size
* visual weight
* alignment

Typical sizes:

```text
16px
18px
20px
24px
```

Do not use oversized icons without a specific reason.

---

# 25. Cards

Cards should use:

```text
Background: #FFFFFF
Border: 1px solid #E5E5EA
Radius: 16px
Shadow: subtle
```

Cards should feel calm and structured.

Avoid creating every section as a floating card.

Some sections should simply sit directly on the page canvas.

---

# 26. Event Cards

Event cards should communicate information in this order:

```text
Date / status
Event title
Venue
Optional short information
Price
Primary action
```

Visual hierarchy should be obvious.

Use event imagery only when real image data is available.

---

# 27. Event Card Placeholder Visuals

If EventPulse currently has no event-image field:

DO NOT use random internet images.

DO NOT insert unrelated stock photos.

Instead use a designed visual header based on:

```text
soft orange background
event date
minimal typography
simple monochrome ticket/calendar icon
subtle geometric detail if necessary
```

Keep it restrained.

---

# 28. Images

When actual event images become available:

* use consistent aspect ratio
* crop using `object-fit: cover`
* avoid stretched images
* use rounded top corners matching card radius
* maintain good contrast with overlays

Recommended event image ratios:

```text
16:9
or
3:2
```

---

# 29. Search Controls

Search bars should look clean and prominent but not oversized.

Typical style:

```text
Background: white
Border: #E5E5EA
Radius: 12–16px
Height: 48–56px
```

Search icon:

Use a monochrome search SVG/icon.

Do NOT use:

```text
🔍
```

emoji.

---

# 30. Forms

Forms should be simple and easy to scan.

Use:

```text
Label
Input
Optional helper/error text
```

Do not rely only on placeholders.

---

# 31. Inputs

Typical inputs:

```text
Height: 44–48px
Border: #E5E5EA
Radius: 10–12px
Background: #FFFFFF
Text: #1D1D1F
```

Focus:

```text
Orange focus ring
```

Avoid oversized inputs unless they have a specific UX purpose.

---

# 32. Form Labels

Labels should be:

```text
14px
font-weight: 500–600
color: #1D1D1F
```

Helper text:

```text
12–14px
color: #86868B
```

---

# 33. Validation

Errors should appear near the affected field.

Use:

```text
#FF3B30
```

Do not show giant generic error banners for a single invalid field.

Do not use red emojis or warning emojis.

Use a professional warning/error icon if needed.

---

# 34. Tables

Admin and organizer pages may use tables where they improve data scanning.

Tables should have:

* white background
* minimal borders
* strong column alignment
* subtle row separators
* comfortable row height
* restrained status badges

Avoid dense spreadsheet-like styling.

---

# 35. Status Badges

Use compact badges.

Examples:

```text
Pending
Approved
Published
Rejected
Confirmed
Cancelled
```

Keep badge styling subtle.

Do not make badges excessively colorful.

Suggested approach:

```text
Pending    → soft neutral / amber
Approved   → subtle success
Published  → subtle orange or success
Rejected   → subtle red
```

---

# 36. Modals

Modal design:

```text
White background
16–20px radius
Subtle border/shadow
Clear title
Concise content
Clearly separated actions
```

Avoid giant full-screen modals unless necessary.

---

# 37. Drawers

Use drawers for contextual workflows where appropriate.

Animation:

```text
350ms cubic-bezier(0.16, 1, 0.3, 1)
```

Keep transitions subtle.

---

# 38. Motion

Default interactive transition:

```css
transition: all 150ms ease-out;
```

Tap/press:

```css
transform: scale(0.97);
transition: transform 200ms cubic-bezier(0.34, 1.56, 0.64, 1);
```

Do not animate every component.

Motion should communicate responsiveness, not serve as decoration.

---

# 39. Hover Behavior

Cards may use subtle hover behavior such as:

```text
slightly stronger shadow
very small translateY(-2px)
border emphasis
```

Do not make cards jump dramatically.

Do not scale cards significantly.

---

# 40. Loading States

Never show a blank content area while loading.

Prefer clean skeletons.

Skeleton styling:

```text
light neutral surfaces
matching expected content dimensions
minimal shimmer
```

Do not use elaborate animated loading graphics.

---

# 41. Empty States

Empty states should be calm and useful.

Example:

```text
No events available yet

New experiences are being prepared.
Check back soon.
```

Optionally include a professional monochrome icon.

Do not use emojis.

---

# 42. Error States

Errors should use human-readable language.

Good:

```text
We couldn't load events right now.
Please try again.
```

Bad:

```text
HTTP ERROR 500 FETCH_FAILED
```

Internal technical errors belong in logs, not visitor-facing UI.

---

# 43. Page Background Strategy

Do not make every section the same white.

Use subtle contrast:

```text
Header       → #FFFFFF
Hero         → #FFFFFF
Content area → #F5F5F7
Cards        → #FFFFFF
Footer       → white or subtle neutral
```

This creates hierarchy without needing heavy decoration.

---

# 44. Hero Sections

Marketing/public pages may have spacious hero areas.

Use:

* large typography
* concise copy
* simple search or CTA
* strong whitespace

Do not automatically add:

* abstract gradients
* floating blobs
* 3D illustrations
* animated particles
* giant icon collections

---

# 45. Admin / Organizer Interfaces

Admin and organizer screens should share the same EventPulse visual system.

Do not suddenly switch to a generic blue dashboard.

Use:

```text
White
Off-white
Dark charcoal
EventPulse orange
Neutral borders
```

Dashboards should feel like part of the same product.

---

# 46. Dashboard Navigation

If side navigation is needed:

```text
Background: white
Border-right: #E5E5EA
```

Navigation labels:

```text
#1D1D1F
```

Inactive:

```text
#86868B
```

Active:

```text
#FF5B00
```

or:

```text
Soft orange background + dark/orange text
```

Do not use a giant dark sidebar unless specifically approved.

---

# 47. Booking Interfaces

Booking and seat-selection screens may become denser than discovery pages.

Still preserve:

* white cards
* orange selected state
* dark typography
* minimal monochrome icons
* consistent radii
* consistent buttons

Selected seat:

```text
#FF5B00
```

Available seat:

```text
#FFFFFF
Border: #E5E5EA
```

Reserved/unavailable:

```text
#E5E5EA
```

Do not introduce unrelated colors for every seat state.

---

# 48. Checkout / Summary Cards

Ticket and payment summary cards should emphasize:

```text
Ticket count
Subtotal
Fees if applicable
Total
Primary action
```

Price hierarchy should be obvious.

Total price may use:

```text
20–24px
font-weight: 700
```

Do not turn checkout into a colorful dashboard.

---

# 49. Authentication Screens

Sign-in/register pages should remain visually simple.

Preferred structure:

```text
Centered or balanced layout
White form card
Minimal branding
Clear headings
Large whitespace
```

Do not add large unnecessary illustrations.

Google sign-in should use proper Google branding only where required.

Do not recolor Google's official identity assets incorrectly.

---

# 50. Content Tone

Interface text should be:

* concise
* professional
* friendly
* direct

Good:

```text
Create Event
View Details
Review Submission
Try Again
No events found
```

Avoid exaggerated marketing language throughout functional screens.

---

# 51. Currency

For Sri Lankan pricing, display:

```text
LKR 5,000
```

or:

```text
From LKR 5,000
```

Use consistent number formatting.

---

# 52. Dates

Never expose raw ISO timestamps to normal users.

Bad:

```text
2026-09-22T20:06:53.787465Z
```

Good:

```text
22 Sep 2026
```

or:

```text
Tue, 22 Sep · 7:30 PM
```

---

# 53. Responsiveness

Every feature must work at:

```text
Desktop
Tablet
Mobile
```

Do not create desktop-only layouts.

---

# 54. Desktop Grid

For event discovery:

```text
3 cards per row
```

where space permits.

---

# 55. Tablet Grid

Prefer:

```text
2 cards per row
```

---

# 56. Mobile Grid

Use:

```text
1 card per row
```

Mobile pages must not horizontally overflow.

---

# 57. Mobile Typography

Large hero typography should scale down appropriately.

Example:

Desktop:

```text
48px
```

Mobile:

```text
32–36px
```

Do not simply preserve large desktop text on a small viewport.

---

# 58. Mobile Navigation

Keep mobile navigation simple.

Use a standard menu icon if navigation becomes too large.

Use a monochrome professional icon.

Do NOT use:

```text
☰ emoji-like decorative replacements
```

if a proper SVG/icon system is available.

---

# 59. Accessibility

Maintain at least basic accessibility standards.

All new components must consider:

* visible keyboard focus
* semantic HTML
* accessible labels
* sufficient color contrast
* keyboard navigation
* meaningful button labels
* alt text for meaningful images
* not relying solely on color

---

# 60. Do Not Communicate State Only Through Color

Example:

Do not communicate:

```text
Red = rejected
Green = approved
```

without text.

Use:

```text
Rejected
Approved
```

with color as secondary reinforcement.

---

# 61. Component Consistency

Before creating a new component:

1. Inspect existing EventPulse components.
2. Reuse an existing component if appropriate.
3. Extend existing design tokens/styles where possible.
4. Only create a new pattern if the current system does not support the use case.

Do not create three visually different button components.

---

# 62. CSS / Styling Organization

Prefer reusable design tokens.

Where appropriate define variables such as:

```css
:root {
  --ep-orange: #FF5B00;
  --ep-orange-hover: #E05000;
  --ep-orange-active: #C44300;
  --ep-orange-soft: #FFF0E6;

  --ep-bg: #F5F5F7;
  --ep-surface: #FFFFFF;

  --ep-text-primary: #1D1D1F;
  --ep-text-secondary: #86868B;

  --ep-border: #E5E5EA;

  --ep-success: #34C759;
  --ep-danger: #FF3B30;

  --ep-radius-card: 16px;
  --ep-radius-control: 12px;

  --ep-shadow-card: 0 2px 8px rgba(0, 0, 0, 0.04);
}
```

Prefer using the shared variables instead of repeating arbitrary colors.

---

# 63. Bootstrap Usage

Bootstrap may be used for:

* responsive grid
* spacing helpers
* utilities
* layout foundations

But the product must NOT look like default Bootstrap.

Avoid relying on default:

```text
btn-primary
card
navbar
badge
form-control
```

styling without EventPulse customization.

Bootstrap is an implementation tool, not the visual identity.

---

# 64. Avoid Generic AI-Generated UI

This section is mandatory.

Do NOT automatically generate interfaces containing:

* purple/blue gradients
* neon gradients
* large floating gradient orbs
* glassmorphism everywhere
* excessive blur
* oversized pills
* random statistics cards
* unnecessary icon circles
* emoji icons
* colorful emoji navigation
* multiple unrelated accent colors
* giant decorative illustrations
* excessive gradients behind headings
* overly large rounded cards
* meaningless badges
* floating abstract shapes
* random testimonial sections
* fake profile avatars
* stock dashboard components that were not requested
* unnecessary "trending" labels
* gratuitous animation
* excessive shadows

The UI should look like it was deliberately designed by a product designer, not generated from a generic AI frontend prompt.

---

# 65. No Emoji UI Rule

This deserves explicit repetition.

Never use colorful system emojis as interface icons.

Forbidden examples include:

```text
📍 Colombo
🎫 Tickets
🔍 Search
📅 Date
👤 Account
⚙️ Settings
💳 Payment
✅ Approved
❌ Rejected
🚀 Continue
🎉 Success
```

Use monochrome SVG/icon equivalents instead.

Example:

```text
location-pin icon + Colombo, LK
```

instead of:

```text
📍 Colombo, LK
```

---

# 66. Icon Color Rule

By default:

```text
Navigation icon → #1D1D1F
Secondary icon  → #86868B
Active icon     → #FF5B00
Disabled icon   → neutral grey
Destructive     → #FF3B30
```

Do not randomly color icons pink, blue, green and purple.

---

# 67. Gradients

Avoid gradients by default.

A subtle orange tonal treatment may be used for decorative event-card placeholders if necessary.

Do NOT use:

```text
purple → blue
pink → orange
cyan → purple
```

gradient backgrounds.

EventPulse is primarily a flat, clean interface.

---

# 68. Decorative Elements

Use decoration only when the interface would otherwise feel excessively empty.

Any decorative element should be:

* subtle
* low contrast
* brand-aligned
* non-distracting

Prefer geometry and whitespace over illustrations.

---

# 69. Navigation Architecture

Navigation must remain predictable.

Public:

```text
Home
Events
Sign In
```

Organizer/admin/customer navigation should only contain functions relevant to that user.

Do not add navigation items merely to make the UI appear complete.

---

# 70. New Feature Workflow

Whenever implementing a new EventPulse frontend feature:

## Step 1

Read this `styleguide.md`.

## Step 2

Inspect existing EventPulse frontend components and pages.

## Step 3

Identify reusable:

* colors
* typography
* button styles
* navigation
* cards
* inputs
* spacing
* icon patterns

## Step 4

Implement the feature within the existing design language.

## Step 5

Verify desktop/tablet/mobile behavior.

## Step 6

Check for visual inconsistencies.

---

# 71. Before Creating a New Visual Pattern

Ask:

```text
Does EventPulse already have a component for this?
```

If yes:

Reuse or extend it.

If no:

Create the smallest reusable pattern required.

Do not invent a new visual style.

---

# 72. Feature-Specific Design Rule

Functional requirements should determine layout.

Do NOT force every page into:

```text
Hero
3 cards
CTA
```

just because that pattern exists on the homepage.

Examples:

Discovery page:

```text
Hero
Search
Event grid
```

Admin review page:

```text
Page heading
Filters
Submission list/table
Review action
```

Create event page:

```text
Page heading
Structured form
Submit action
```

Booking page:

```text
Event information
Selection interface
Summary
Checkout action
```

Different workflows may use different layouts while preserving the same visual system.

---

# 73. Visual Hierarchy Rule

Every screen should clearly answer:

1. Where am I?
2. What is the most important information?
3. What action should I take next?

Use:

```text
Typography
Spacing
Contrast
Position
Orange accent
```

to establish hierarchy.

---

# 74. Primary Action Rule

Most screens should have one obvious primary action.

Example:

```text
Create Event
Submit Event
Approve
Proceed to Checkout
Save Changes
```

Do not display three equally prominent orange buttons unless the workflow truly requires them.

---

# 75. Secondary Actions

Secondary actions should be visually quieter.

Use:

```text
white button
neutral border
dark text
```

or text links where appropriate.

---

# 76. Destructive Actions

Destructive actions should be isolated from primary actions.

Do not place:

```text
Save
Delete
```

as visually identical adjacent buttons.

---

# 77. Content Density

Public pages:

```text
Low to medium density
Large whitespace
```

Admin/organizer management pages:

```text
Medium density
Efficient information scanning
```

Do not make dashboards unnecessarily sparse or public pages unnecessarily dense.

---

# 78. Data Visualization

If charts are introduced later:

Use minimal professional charts.

Prefer:

```text
orange
dark charcoal
neutral greys
```

Do not use rainbow chart palettes unless multiple categories genuinely require differentiation.

---

# 79. Footer

If a footer is needed:

Keep it simple.

Possible content:

```text
EventPulse
Navigation
Support
Copyright
```

Use a white or neutral background.

Do not create an enormous marketing footer unless required.

---

# 80. Final Visual Test

Before considering a frontend feature complete, verify:

* Does it look like the existing EventPulse homepage?
* Is orange used selectively?
* Is typography consistent?
* Is spacing consistent?
* Are cards/radii consistent?
* Are icons monochrome and professional?
* Are there any colorful emojis? If yes, remove them.
* Are gradients unnecessary? If yes, remove them.
* Does anything look like a generic AI-generated template?
* Does Bootstrap default styling visibly dominate?
* Does the page work on mobile?
* Is the primary action obvious?
* Are loading, empty and error states handled?
* Is the interface accessible?

---

# 81. Mandatory Instruction for AI Coding Tools

Whenever an AI coding tool such as Antigravity implements a frontend feature:

> **Read and follow this entire `styleguide.md` before modifying frontend UI.**

The existing EventPulse UI is the design baseline.

Do not redesign existing components merely because another style might also look good.

New UI must integrate with existing UI.

When modifying frontend code:

1. Preserve existing working design patterns.
2. Reuse existing components where sensible.
3. Use EventPulse design tokens.
4. Use monochrome professional icons.
5. Never use colorful emojis as icons.
6. Avoid generic AI-generated visual patterns.
7. Maintain responsive behavior.
8. Maintain accessibility.
9. Do not introduce a new UI library without a clear technical need.
10. Keep visual changes scoped to the feature being implemented.

If a requirement conflicts with this guide, preserve functional correctness first and adapt the layout while maintaining the EventPulse visual identity.

---

# 82. EventPulse Visual Identity Summary

EventPulse should consistently look like:

```text
Clean white / off-white surfaces
+
Dark Apple-style typography
+
EventPulse orange accent
+
Subtle neutral borders
+
Soft shadows
+
16px cards
+
12px controls
+
Generous spacing
+
Professional monochrome icons
+
Minimal decoration
+
Clear visual hierarchy
```

Never:

```text
Colorful emojis
Rainbow icons
Purple AI gradients
Glassmorphism everywhere
Neon effects
Random decorative blobs
Generic dashboard templates
Excessive pills
Over-animation
Unnecessary visual complexity
```

The final product should feel intentionally designed, restrained, consistent, and credible enough to present as a real event-ticketing application.
