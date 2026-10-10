# Screenshots

The images the root `README.md` embeds. How they were taken, so regenerating them is not guesswork:

- **A production build**, not `nuxt dev` — `npm run build` then `node .output/server/index.mjs`.
  The dev server draws a devtools badge over the bottom of every page, and it is not part of the
  application.
- **A freshly seeded database**, so the contents are the ones `DatabaseInitialiser` creates: three
  languages, two categories, three products. Not a working database, which would bake whatever was
  being tested that day into the repository.
- **Placeholder imagery**, generated rather than photographed, attached as the products' `primary`
  and `gallery`, the categories' `banner` and branding's `logo`, with alt text written in English,
  Arabic and French — so the pages show the composition rather than empty slots.
- **1400px wide, captured at 2× and resized to 1800px**, which is about twice GitHub's content
  column and therefore still sharp on the displays most people read a README on.

`catalog.png` and `catalog-rtl.png` are the same page and the same data, read in `en` and in `ar`.
That pair is the point of the sample, so it is worth keeping them in step if either is replaced.
