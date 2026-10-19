# Swordsman animation native pose review

Built-in image generation; exactly one transparent call per new pose. Approved idle is untouched. All generated masters remain native 1585 x 992 RGBA without rescaling, cropping, translation, or alpha modification. Each pose folder contains master.png, prompt.txt, and alpha-qa.json.

- walk-a: passes distinct gait contact. Viewer-right boot forward, viewer-left boot trailing. Helmet top 105; planted boot bottom 909, measured with alpha >=128. Visible bounds [318,105,1310,910].
- attack: passes clear thrust silhouette. Sword arm extended across body; sword tip right and upward, shield tucked behind. Helmet top 102; planted boot bottom 889. Visible bounds [371,102,1486,890].
- walk-b: passes opposite gait contact. Viewer-left boot forward and large in foreground, viewer-right boot trailing and raised. Helmet top 101; planted boot bottom 904. Visible bounds [305,101,1268,905].

All poses preserve the oversized helmet/head/glove and broad sword identity, blue cloth, round shield, thick contour and matte faceted style. No duplicate weapons, smear or extra limbs observed. Tiny alpha residue extends beyond visible character bounds; use threshold bounds for QA, and retain native originals. Walking boots depart from requested planted baseline887 by22 and17 pixels; parent integrating shared-scale export should explicitly record any bounded translation registration, never independent fit/crop/scale.
