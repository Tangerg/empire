import { candidate01AssetUrl } from './candidate-01-assets';

export const CANDIDATE_01_PORTRAITS: Readonly<Record<string, string>> = {
  laiya: candidate01AssetUrl('C01-CHAR-LEIA-01'),
  roderick: candidate01AssetUrl('C01-CHAR-RODERICK-01'),
  cain: candidate01AssetUrl('C01-CHAR-CAIN-01'),
  mirelle: candidate01AssetUrl('C01-CHAR-MIREL-01'),
  bran: candidate01AssetUrl('C01-CHAR-BRAN-01'),
  tasha: candidate01AssetUrl('C01-CHAR-TASHA-01'),
  ivra: candidate01AssetUrl('C01-CHAR-IVRA-01'),
};

export const CANDIDATE_01_MENU_ART = candidate01AssetUrl('C01-CH01-S05');

/** Resolve a semantic narrative-art id at the presentation boundary. */
export const candidate01StoryArt = (topicId: string): string => candidate01AssetUrl(topicId);
