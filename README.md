# My First Umbraco Site

A small multi-page site built with Umbraco CMS 13 (.NET 8) to learn core CMS concepts: content modeling, templating, and basic backend form handling.

## Structure

- **Home Page** — landing page with a title and rich text body
- **Blog Listing → Blog Post** — a parent/child content structure. Blog Listing restricts its allowed children to Blog Post only, enforcing the site's content architecture at the schema level. The listing template loops through and sorts children by publish date automatically.
- **Contact Page** — a form handled by a custom Surface Controller (`ContactSurfaceController`), including basic server-side validation and a POST-Redirect-GET pattern to avoid duplicate form submissions on refresh.

## Key patterns used

- **Shared Layout** (`_layout.cshtml`) — all page templates render inside one shared shell (nav, `<head>`, styling), avoiding repeated boilerplate
- **Partial View** (`Navigation.cshtml`) — one reusable nav component, dynamically listing top-level pages via `Umbraco.ContentAtRoot()`
- **SEO fields** — every Document Type includes `SeoTitle` and `MetaDescription`, rendered dynamically in the Layout's `<head>`
- **Media Picker** — Blog Posts support an optional featured image, rendered conditionally

## Tech stack

Umbraco CMS 13 (LTS), .NET 8, Razor views, SQLite

## Running locally