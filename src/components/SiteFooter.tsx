const copyrightYear = new Date().getFullYear();

export function SiteFooter() {
  return (
    <footer className="border-line text-muted mt-8 border-t py-8 text-sm">
      <p>© {copyrightYear} Seth Stearns. All rights reserved.</p>
    </footer>
  );
}
