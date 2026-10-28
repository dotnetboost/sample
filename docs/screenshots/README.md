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
  Arabic and French — so the pages show the composition rather than empty slots. Branding's store
  name is given an Arabic and a French translation too, so its card reads "every language covered".
- **1400px wide, captured at 2× and resized to 1800px**, which is about twice GitHub's content
  column and therefore still sharp on the displays most people read a README on. A 1400×760
  viewport in the light theme; `overview.png`, `settings.png` and `audit.png` are the full page,
  the other three the viewport. Playwright's Chromium with `deviceScaleFactor: 2`, then
  `sips --resampleWidth 1800`.
- **In this order**: the imagery, then every image except `audit.png`, then the writes below, then
  `audit.png`. The writes delete the lamp and add a bookshelf, so photographing the catalogue after
  them would show neither the seeded three nor their images.

`catalog.png` and `catalog-rtl.png` are the same page and the same data, read in `en` and in `ar`.
That pair is the point of the sample, so it is worth keeping them in step if either is replaced.

## `audit.png` also needs a trail

A freshly seeded database has only the seed's own: `DatabaseInitialiser` saves through the audited
`DbContext`, so the trail opens with a `Created` entry per row and no actor — "unknown" on the page.
That is honest and says nothing about who changed what, which is what the page is for.

These six writes are what it shows. They are deliberate rather than whatever was being tested that
day, for the same reason the database is a seeded one, and they are run against it with the API up:

```bash
API=${API:-http://localhost:5180}

# Re-posts a product with its own translations and whatever this call changes. A PUT is a full
# replacement, and every write below is meant to move columns only.
repost() {
  actor=$1; id=$2; patch=$3
  body=$(curl -s "$API/api/products/$id" | python3 -c '
import json, sys
product = json.load(sys.stdin)
product.update(json.loads(sys.argv[1]))
print(json.dumps({k: product[k] for k in ("sku", "price", "categoryId", "translations")}))' "$patch")

  curl -s -o /dev/null -X PUT "$API/api/products/$id" -H 'Content-Type: application/json' \
    ${actor:+-H "X-Sample-Actor: $actor"} -d "$body"
}

# 1. bob adds a product.
curl -s -o /dev/null -X POST "$API/api/products" -H 'Content-Type: application/json' \
  -H 'X-Sample-Actor: bob@example.com' -d '{
    "sku": "SHELF-001", "price": 129.00, "categoryId": 1,
    "translations": {
      "en": { "Name": "Ash bookshelf", "Description": "Five shelves, open back." },
      "ar": { "Name": "رف كتب من الدردار", "Description": "خمسة رفوف، بظهر مفتوح." },
      "fr": { "Name": "Bibliothèque en frêne", "Description": "Cinq étagères, dos ouvert." }
    }}'

# 2. The default sample user corrects its SKU — no header, so it is recorded as sample.user@example.com.
repost "" 4 '{"sku": "SHELF-002"}'

# 3. carol withdraws the lamp. Its history outlives the row.
curl -s -o /dev/null -X DELETE "$API/api/products/3" -H 'X-Sample-Actor: carol@example.com'

# 4. alice renames a category's slug.
curl -s -o /dev/null -X PUT "$API/api/categories/2" -H 'Content-Type: application/json' \
  -H 'X-Sample-Actor: alice@example.com' -d "$(curl -s "$API/api/categories/2" | python3 -c '
import json, sys
category = json.load(sys.stdin)
category["slug"] = "lighting-and-lamps"
print(json.dumps({k: category[k] for k in ("slug", "translations")}))')"

# 5. dana rewrites the chair IN ARABIC ONLY — and the trail records nothing at all, because
#    Name and Description carry [AuditIgnore].
dana=$(curl -s "$API/api/products/2" | python3 -c '
import json, sys
product = json.load(sys.stdin)
product["translations"]["ar"] = { "Name": "كرسي من الجوز", "Description": "جوز مصقول، بمسند ظهر منحنٍ." }
print(json.dumps({k: product[k] for k in ("sku", "price", "categoryId", "translations")}))')
curl -s -o /dev/null -X PUT "$API/api/products/2" -H 'Content-Type: application/json' \
  -H 'X-Sample-Actor: dana@example.com' -d "$dana"

# 6. alice reprices the oak desk. Newest entry, and the one the screenshot opens.
repost alice@example.com 1 '{"price": 389.95}'
```

The fifth is the one worth keeping if these are ever rewritten: it is a write that adds **no entry**,
which is exactly what the rules card beside the entries explains.

The page is then photographed with the entity filter on `Product` and the newest entry opened.

It is **taller than the others**, because the rules card and an opened entry do not fit in their
height. 1400px wide at 2× is the part that is fixed; the height is whatever the page needs with
nothing left to scroll to.

It needs no imagery of its own: no media is drawn on that page, and the filter keeps the uploads'
own entries out of the picture.
