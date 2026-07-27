# Acceptance Criteria

## 1. General rules

A feature is accepted only if:
- It matches the linked requirement section.
- It compiles without introducing unrelated build errors.
- It does not break controller navigation.
- It presents explicit error or fallback behavior when required.
- It does not silently ignore unsupported server capabilities.
- It respects light/dark theming where applicable.

## 2. First-launch setup

### Pass if:
- First launch shows Server URL, Username, and Password fields.
- Password is masked.
- Entering a URL without a scheme sanitizes to https://.
- Leading/trailing whitespace is removed before connect.
- Trailing slashes are removed before connect.
- Invalid URL shape produces inline validation feedback.
- Bad credentials produce a visible authentication error.
- Unreachable server produces a visible connection error.
- A recent server can be selected without retyping the full address, if previously used.
- Keyboard/paste input does not break the flow when available.
- Password/token is stored in the Credential Locker (PasswordVault), not plain settings.

### Fail if:
- The app attempts connection using raw unsanitized input.
- Errors appear only in logs or debug output.
- Password text is visible in plain text.
- Secrets appear in local settings files or logs.
- QR pairing is shown anywhere in the setup flow.

## 3. Navigation shell

### Pass if:
- Left navigation is persistent and collapsible.
- Search and Home are always visible.
- Artists, Albums, and Playlists can each be hidden independently from Settings.
- Search filters appear in the main pane only after search is active.
- Focus navigation through the left menu is consistent with controller input.
- Account/server info is placed in the bottom area of the shell.

### Fail if:
- Search/Home can be hidden.
- Search filters are embedded permanently inside the nav.
- Controller focus order is ambiguous or skips entries.

## 4. Home view

### Pass if:
- Home shows "Pick up where you left off" when persisted playback context exists.
- Resume action restores queue/context rather than starting unrelated content.
- Most Played uses album data from the frequent album endpoint.
- Recently Added uses album data from the newest album endpoint.
- Random Picks uses album data from the random album endpoint.
- Carousels are horizontal and non-looping.
- The layout presents multiple items on-screen in a TV-friendly way.

### Fail if:
- Home carousels mix arbitrary playlists into album slots.
- Resume cannot restore prior context.
- Carousels loop endlessly without a requirement change.

## 5. Browse views

### Artists
Pass if:
- Default view is grid.
- Alternate list view exists.
- View-mode toggle is clearly visible, names the mode it is currently showing,
  and reads as part of the app's own palette rather than as a switched-on
  system control.
- List rows show the album count and mark favorites.
- List rows also show each artist's track count and total play time, summed from
  the album list rather than fetched per artist.
- Those figures appear only once the whole album list has been read; a row never
  shows a partial sum.
- If that pass fails or the user navigates away mid-pass, rows still show their
  album count — never a zero, and never an error state over a page that loaded.

### Albums
Pass if:
- Default view is grid.
- Alternate list view exists.
- View-mode toggle is clearly visible (same criteria as Artists).
- List rows show year, genre, track count and total play time where the server
  provided them, and mark favorites.
- A row whose album carries none of that metadata shows no empty separators or
  zeros.

### Songs
Pass if:
- Songs view is list-only.
- Columns include title, artist, album, length, format, bitrate.
- A sort control offers Default Order, Title, Artist, Album, and Date Added, and the chosen sort is shown on the control.
- Default Order reproduces the server's own order.
- Play All replaces the queue with the whole library in the current sort order; Shuffle All replaces it with the whole library randomized.
- While the library is still loading, the sort control and both library actions are disabled with a visible reason, and rows already loaded remain playable.
- Date Added is unavailable (not silently meaningless) on a server that omits `created`.

### Playlists
Pass if:
- Default view is grid.
- Alternate list view exists.
- Playlist artwork is generated client-side from constituent album art.
- Single-album playlists collapse to a single-cover representation.
- List rows show owner, the playlist's description when it has one, track count
  and total play time, and no starred marker.

### Favorites (§7.5)
Pass if:
- The Favorites nav item opens a view with Artists, Albums and Songs tabs, and
  a first visit lands on **Songs** (the third tab, not the first).
- Each tab lists the starred items of that scope, using the browse card
  language (artists/albums) and the §7.3 columns (songs).
- The last selected tab is restored on return and after an app restart.
- The sort options offered change with the tab, and each tab's chosen sort is
  restored independently.
- Changing the sort reorders the whole scope immediately, with no refetch.
- Selecting a song replaces the queue with the favorite songs in their current
  sort order, positioned at that song.
- Play All replaces the queue with the whole active tab in its current sort
  order; Shuffle All does the same with the pool randomized.
- While Play All / Shuffle All is gathering songs on the Artists or Albums
  tab, both actions and the sort control are disabled and progress is visible.
- Every result carries a context menu with at least Play, Play next, Add to
  queue and Remove from favorites; songs additionally offer Go to album and
  Go to artist, albums offer Go to artist.
- Removing any item from favorites removes it from the list.
- Each scope with no favorites shows its own explicit empty state.
- A server older than API 1.8 shows an explicit unsupported state, not an
  empty or broken list.

### Fail if:
- Grid/list toggle is hidden in a submenu.
- Songs view omits required metadata columns.
- A Songs sort or Play All / Shuffle All acts on only part of the library while presenting itself as covering all of it.
- Playlist artwork depends on a nonexistent server endpoint.
- Favorites sorts only the visible or partially loaded portion of a scope.
- A tab or sort choice is forgotten between visits.
- An item stays in the Favorites list after being unfavorited there.
- Play All / Shuffle All covers only part of the active tab, or appears inert
  while it is gathering songs.

## 6. Artist detail

### Pass if:
- Artist detail shows artwork, name, favorite toggle, album count, and song count.
- The counts line also shows total play time and the years the albums span,
  omitting either when the server sent nothing to compute it from.
- Favorite toggle updates persisted star state correctly.
- Play replaces the queue with artist songs in oldest-album-first order.
- Shuffle replaces the queue with the same song pool in randomized order.
- Starting playback reveals the now-playing bar.
- Lower section shows albums using the browse-view visual language.

### Fail if:
- Play appends instead of replacing.
- Shuffle uses a different song pool.
- Favorite action does not persist.

## 7. Album detail

### Pass if:
- Album detail visually mirrors artist detail where appropriate.
- Artwork, title, artist, year, favorite toggle, song count, and total duration are visible, plus genre when the server tagged one.
- Play replaces queue with album tracks only.
- Shuffle replaces queue with album tracks only in randomized order.
- Track list is shown in original disc/track order.
- Current track is visually highlighted when applicable.
- Selecting a track jumps playback directly.

### Fail if:
- Album detail uses a different unrelated layout.
- Track order is lost.
- Play/Shuffle appends to the existing queue.

## 8. Now-playing bar and queue

### Pass if:
- Playback shows a persistent bottom now-playing bar.
- Compact bar shows thumbnail, title, artist, album, format, bitrate, elapsed, total, and required controls.
- Expand/collapse animates.
- Expanded state shows rich metadata/artwork.
- Queue view supports direct jumping to any queued track.
- Queue expansion supports a two-pane playback + up-next layout.
- Y button mapping is implemented or intentionally documented as pending due to platform/input constraints.

### Fail if:
- Queue cannot be inspected during playback.
- Expanded view is modal-only with no direct track jump.
- New Play actions append rather than replace.

## 8.1 Lyrics (§10.1)

### Pass if:
- When the server supports the `songLyrics` extension, lyrics are reachable from the now-playing experience (a bar lyrics control and a Lyrics view in the expanded overlay).
- Synced lyrics highlight the current line and auto-scroll to follow playback.
- The controller can move the selection through the lyrics (read ahead/behind) without that movement alone changing playback, and can return to / resume following the current line without moving playback.
- Selecting a synced line (A/Enter/click) seeks playback to that line and following resumes from there.
- Unsynced lyrics display as readable scrollable text with no highlight, and selecting a line falls back to returning to the current line rather than doing nothing.
- A track with no lyrics on a supporting server shows an explicit "no lyrics" state.
- Lyrics update when the track changes.

### Fail if:
- The lyrics UI appears (broken/empty) on a server that does not support the extension.
- Merely moving the selection through the lyrics changes playback.
- Selecting a synced line leaves playback where it was.
- Selecting the last synced line skips to the next track instead of seeking.
- A missing-lyrics track shows a blank pane or an error instead of an explicit state.

## 9. Background playback

### Pass if:
- Playback continues in background within supported Xbox/UWP behavior.
- SMTC metadata and transport controls reflect current playback state.
- App handles background memory pressure without obvious instability.
- Manifest/capability setup is correct for the chosen background implementation.

### Fail if:
- Playback stops immediately on background without known platform cause.
- SMTC is missing or stale.
- Memory-pressure events are ignored.

## 10. Settings

### Pass if:
- Theme options include Automatic, Dark, Light.
- User selection persists across relaunch.
- Automatic follows system theme.
- Nav visibility toggles work for Artists, Albums, Playlists only.
- Search and Home remain fixed.

### Fail if:
- Theme resets every launch.
- Search/Home can be hidden.

## 11. Input and theming

### Pass if:
- Controller is the primary tested interaction path.
- Keyboard/mouse input does not break the app.
- Input glyphs switch based on active input mode, if implemented in the current slice.
- All implemented screens render acceptably in both light and dark themes.
- Focus indicators remain visible at TV distance.

### Fail if:
- Controller navigation is treated as secondary.
- Focus states are too subtle to track.
- Light/dark theming breaks layout or contrast.

## 12. Server capability fallback behavior

### Pass if:
- Unsupported server capabilities are detected explicitly.
- UI hides/disables dependent features intentionally.
- A legacy-compatible path is used where defined.

### Fail if:
- Features disappear silently with no fallback reasoning in code or UX.
- The client hard-fails solely because one optional extension is absent.

## 13. Definition of done per slice

Before marking any slice complete, confirm:
- Requirement sections implemented:
- Acceptance criteria checked:
- Build status:
- Manual navigation test performed:
- Manual playback test performed, if relevant:
- Known gaps / TODOs:
- Docs updated:
