# 0008 — Auth Identity par cookie + durcissement sécurité

**Statut :** Accepté

## Contexte
L'application authentifiée (coordinateurs/admin) est une app web server-rendered ; le formulaire
d'inscription public est anonyme et **embarquable en iframe** sur des sites partenaires. Il faut
sécuriser sans casser l'iframe.

## Décision
- **ASP.NET Core Identity, schéma cookie**, rôles `Admin` / `Coordinator`. `ICurrentUserService`
  lit `UserId` / `OrganisationId` / `Role` depuis les claims.
- **Durcissement** : `UseForwardedHeaders` (l'app voit HTTPS derrière le proxy Caddy) → HSTS effectif ;
  middleware d'en-têtes de sécurité (`X-Content-Type-Options`, `Referrer-Policy`,
  `X-Frame-Options: SAMEORIGIN` **sauf** `/PublicRegistration` qui reste *framable*) ; header `Server`
  retiré ; cookie d'auth `HttpOnly` + `SameSite=Lax` + `Secure` (Always en prod, SameAsRequest en dev) ;
  antiforgery sur les POST ; **rate limiting** par IP (login/register 10/min, inscription publique 30/min).

## Conséquences
- L'app authentifiée est protégée du clickjacking ; l'iframe public reste intégrable chez les partenaires.
- Brute-force/spam limités sur les endpoints anonymes sensibles.
- Une CSP de contenu est désormais active (`SecurityHeadersMiddleware`), limitée aux CDN réellement
  utilisés (jQuery, Bootstrap, Summernote) ; l'entrée `cdn.tiny.cloud` a été retirée (TinyMCE n'a
  jamais été réellement intégré — Summernote est l'éditeur riche utilisé).

## Alternatives écartées
- **JWT/OIDC** : inutilement complexe pour une app MVC server-rendered solo.
- **`X-Frame-Options: DENY` global** : casserait l'iframe d'inscription publique.
- **`SameSite=Strict`** : friction sur les liens entrants ; `Lax` + antiforgery suffit.
