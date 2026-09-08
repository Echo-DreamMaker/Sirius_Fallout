# #Misfits Add - Localization for the /raid request system (player window + admin tab).

raid-request-window-title = Raid Request

# Header / eligibility
raid-request-your-faction = Your faction: { $faction }
raid-request-no-faction = You don't currently belong to a recognized faction.
raid-request-individual-suffix = (individual request)
raid-request-individual-banner = As a Wastelander you may submit individual raid requests. Only you will be notified of the admin's decision.
raid-request-not-eligible = Your faction ({ $faction }) is not permitted to submit raid requests.

# Submission form
raid-request-submit-header = Submit a raid request
raid-request-target-label = Target faction or group:
raid-request-location-label = Location (optional):
raid-request-location-placeholder = e.g. NCR Outpost Bravo, the southern dunes...
raid-request-reason-label = Reason / plan:
raid-request-reason-placeholder = Explain who you intend to raid and why. Minimum five words.
raid-request-submit-button = Submit request
raid-request-submit-confirm = Press again to confirm
raid-request-confirm-prompt = Click again to send this request to the admins.
raid-request-reason-too-short = Your reason must be at least { $min } words.

# My-requests history (player side)
raid-request-my-requests-header = Your requests this round
raid-request-no-my-requests = You haven't submitted any raid requests this round.

# Admin embedded panel (bwoink Raid tab)
raid-request-filter-pending = Pending
raid-request-filter-decided = Decided
raid-request-filter-all = All
raid-request-list-empty = (no requests match this filter)
raid-request-no-selection = Select a request to view details.
raid-request-admin-comment-label = Remarks (sent to faction or group):
raid-request-admin-comment-placeholder = Required. This message is delivered to every notified player.
raid-request-approve-button = Approve
raid-request-deny-button = Deny
raid-request-end-button = End Raid
raid-request-comment-required = A comment is required before approving or denying.
raid-request-pending-count = { $count } pending raid request(s)
raid-request-no-pending = No pending raid requests.

# Decision popups (client-side)
raid-request-popup-approved = Raid APPROVED: { $from } → { $to }
raid-request-popup-denied = Raid DENIED: { $from } → { $to }
raid-request-popup-target-warning = Incoming raid threat APPROVED against { $target }!

# #Misfits Add - Mandatory acknowledgement shown when a raid concludes.
raid-concluded-window-title = Raid Over
raid-concluded-header = RAID #{ $id } HAS ENDED
raid-concluded-parties = { $from } vs { $to }
raid-concluded-warning = Combat authorization has ended. Stop all raid-related hostilities immediately.
raid-concluded-acknowledge = I acknowledge

# #Misfits Add - Peer-faction / peer-group approval popup (target leader bypass).
raid-request-peer-window-title = Raid Threat — Decide
raid-request-peer-header = Incoming raid request from { $faction } against { $target }.
raid-request-peer-from-label = From:
raid-request-peer-location-label = Stated location:
raid-request-peer-location-unset = (not specified)
raid-request-peer-reason-label = Reason / plan:
raid-request-peer-comment-label = Your remarks (sent to the requester's faction or group):
raid-request-peer-comment-placeholder = Optional. Will be shown verbatim to the other faction or group.
raid-request-peer-approve-button = Allow Raid
raid-request-peer-deny-button = Refuse
