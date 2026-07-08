#!/bin/sh
# Downloads Piper TTS voice models into the mounted voices directory.
# Idempotent: only downloads files that are not already present, so it's safe to
# run on every container start. Set COPYCAT_TTS_VOICES_DIR to override the target.
set -eu

DEST="${COPYCAT_TTS_VOICES_DIR:-/app/piper-voices}"
BASE="https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0"

mkdir -p "$DEST"

# dl <remote-path> <local-filename>
dl() {
    target="$DEST/$2"
    if [ -s "$target" ]; then
        return 0
    fi
    echo "[piper-voices] downloading $2"
    # Download to a temp file first so an interrupted download doesn't leave a
    # truncated model that would be skipped next time.
    if curl -fsSL "$BASE/$1" -o "$target.tmp"; then
        mv "$target.tmp" "$target"
    else
        rm -f "$target.tmp"
        echo "[piper-voices] WARNING: failed to download $2" >&2
    fi
}

# --- US English ---
dl en/en_US/amy/medium/en_US-amy-medium.onnx              en_US-amy-medium.onnx
dl en/en_US/amy/medium/en_US-amy-medium.onnx.json         en_US-amy-medium.onnx.json
dl en/en_US/bryce/medium/en_US-bryce-medium.onnx          en_US-bryce-medium.onnx
dl en/en_US/bryce/medium/en_US-bryce-medium.onnx.json     en_US-bryce-medium.onnx.json
dl en/en_US/danny/low/en_US-danny-low.onnx                en_US-danny-low.onnx
dl en/en_US/danny/low/en_US-danny-low.onnx.json           en_US-danny-low.onnx.json
dl en/en_US/hfc_female/medium/en_US-hfc_female-medium.onnx      en_US-hfc_female-medium.onnx
dl en/en_US/hfc_female/medium/en_US-hfc_female-medium.onnx.json en_US-hfc_female-medium.onnx.json
dl en/en_US/hfc_male/medium/en_US-hfc_male-medium.onnx         en_US-hfc_male-medium.onnx
dl en/en_US/hfc_male/medium/en_US-hfc_male-medium.onnx.json    en_US-hfc_male-medium.onnx.json
dl en/en_US/joe/medium/en_US-joe-medium.onnx              en_US-joe-medium.onnx
dl en/en_US/joe/medium/en_US-joe-medium.onnx.json         en_US-joe-medium.onnx.json
dl en/en_US/john/medium/en_US-john-medium.onnx            en_US-john-medium.onnx
dl en/en_US/john/medium/en_US-john-medium.onnx.json       en_US-john-medium.onnx.json
dl en/en_US/kathleen/low/en_US-kathleen-low.onnx          en_US-kathleen-low.onnx
dl en/en_US/kathleen/low/en_US-kathleen-low.onnx.json     en_US-kathleen-low.onnx.json
dl en/en_US/kristin/medium/en_US-kristin-medium.onnx      en_US-kristin-medium.onnx
dl en/en_US/kristin/medium/en_US-kristin-medium.onnx.json en_US-kristin-medium.onnx.json
dl en/en_US/kusal/medium/en_US-kusal-medium.onnx          en_US-kusal-medium.onnx
dl en/en_US/kusal/medium/en_US-kusal-medium.onnx.json     en_US-kusal-medium.onnx.json
dl en/en_US/lessac/high/en_US-lessac-high.onnx            en_US-lessac-high.onnx
dl en/en_US/lessac/high/en_US-lessac-high.onnx.json       en_US-lessac-high.onnx.json
dl en/en_US/ljspeech/high/en_US-ljspeech-high.onnx        en_US-ljspeech-high.onnx
dl en/en_US/ljspeech/high/en_US-ljspeech-high.onnx.json   en_US-ljspeech-high.onnx.json
dl en/en_US/norman/medium/en_US-norman-medium.onnx        en_US-norman-medium.onnx
dl en/en_US/norman/medium/en_US-norman-medium.onnx.json   en_US-norman-medium.onnx.json
dl en/en_US/ryan/high/en_US-ryan-high.onnx                en_US-ryan-high.onnx
dl en/en_US/ryan/high/en_US-ryan-high.onnx.json           en_US-ryan-high.onnx.json

# --- UK English ---
dl en/en_GB/alan/medium/en_GB-alan-medium.onnx                              en_GB-alan-medium.onnx
dl en/en_GB/alan/medium/en_GB-alan-medium.onnx.json                         en_GB-alan-medium.onnx.json
dl en/en_GB/alba/medium/en_GB-alba-medium.onnx                              en_GB-alba-medium.onnx
dl en/en_GB/alba/medium/en_GB-alba-medium.onnx.json                         en_GB-alba-medium.onnx.json
dl en/en_GB/aru/medium/en_GB-aru-medium.onnx                                en_GB-aru-medium.onnx
dl en/en_GB/aru/medium/en_GB-aru-medium.onnx.json                           en_GB-aru-medium.onnx.json
dl en/en_GB/cori/high/en_GB-cori-high.onnx                                  en_GB-cori-high.onnx
dl en/en_GB/cori/high/en_GB-cori-high.onnx.json                             en_GB-cori-high.onnx.json
dl en/en_GB/jenny_dioco/medium/en_GB-jenny_dioco-medium.onnx                en_GB-jenny_dioco-medium.onnx
dl en/en_GB/jenny_dioco/medium/en_GB-jenny_dioco-medium.onnx.json           en_GB-jenny_dioco-medium.onnx.json
dl en/en_GB/northern_english_male/medium/en_GB-northern_english_male-medium.onnx      en_GB-northern_english_male-medium.onnx
dl en/en_GB/northern_english_male/medium/en_GB-northern_english_male-medium.onnx.json en_GB-northern_english_male-medium.onnx.json
dl en/en_GB/southern_english_female/low/en_GB-southern_english_female-low.onnx      en_GB-southern_english_female-low.onnx
dl en/en_GB/southern_english_female/low/en_GB-southern_english_female-low.onnx.json en_GB-southern_english_female-low.onnx.json

# --- Other languages (read English with an accent) ---
dl ar/ar_JO/kareem/medium/ar_JO-kareem-medium.onnx                ar_JO-kareem-medium.onnx
dl ar/ar_JO/kareem/medium/ar_JO-kareem-medium.onnx.json           ar_JO-kareem-medium.onnx.json
dl ca/ca_ES/upc_ona/medium/ca_ES-upc_ona-medium.onnx              ca_ES-upc_ona-medium.onnx
dl ca/ca_ES/upc_ona/medium/ca_ES-upc_ona-medium.onnx.json         ca_ES-upc_ona-medium.onnx.json
dl cs/cs_CZ/jirka/medium/cs_CZ-jirka-medium.onnx                  cs_CZ-jirka-medium.onnx
dl cs/cs_CZ/jirka/medium/cs_CZ-jirka-medium.onnx.json             cs_CZ-jirka-medium.onnx.json
dl cy/cy_GB/gwryw_gogleddol/medium/cy_GB-gwryw_gogleddol-medium.onnx      cy_GB-gwryw_gogleddol-medium.onnx
dl cy/cy_GB/gwryw_gogleddol/medium/cy_GB-gwryw_gogleddol-medium.onnx.json cy_GB-gwryw_gogleddol-medium.onnx.json
dl da/da_DK/talesyntese/medium/da_DK-talesyntese-medium.onnx      da_DK-talesyntese-medium.onnx
dl da/da_DK/talesyntese/medium/da_DK-talesyntese-medium.onnx.json da_DK-talesyntese-medium.onnx.json
dl de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx            de_DE-thorsten-medium.onnx
dl de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx.json       de_DE-thorsten-medium.onnx.json
dl el/el_GR/rapunzelina/low/el_GR-rapunzelina-low.onnx            el_GR-rapunzelina-low.onnx
dl el/el_GR/rapunzelina/low/el_GR-rapunzelina-low.onnx.json       el_GR-rapunzelina-low.onnx.json
dl es/es_ES/davefx/medium/es_ES-davefx-medium.onnx                es_ES-davefx-medium.onnx
dl es/es_ES/davefx/medium/es_ES-davefx-medium.onnx.json           es_ES-davefx-medium.onnx.json
dl es/es_MX/ald/medium/es_MX-ald-medium.onnx                      es_MX-ald-medium.onnx
dl es/es_MX/ald/medium/es_MX-ald-medium.onnx.json                 es_MX-ald-medium.onnx.json
dl es/es_AR/daniela/high/es_AR-daniela-high.onnx                  es_AR-daniela-high.onnx
dl es/es_AR/daniela/high/es_AR-daniela-high.onnx.json             es_AR-daniela-high.onnx.json
dl fa/fa_IR/gyro/medium/fa_IR-gyro-medium.onnx                    fa_IR-gyro-medium.onnx
dl fa/fa_IR/gyro/medium/fa_IR-gyro-medium.onnx.json               fa_IR-gyro-medium.onnx.json
dl fi/fi_FI/harri/medium/fi_FI-harri-medium.onnx                  fi_FI-harri-medium.onnx
dl fi/fi_FI/harri/medium/fi_FI-harri-medium.onnx.json             fi_FI-harri-medium.onnx.json
dl fr/fr_FR/siwis/medium/fr_FR-siwis-medium.onnx                  fr_FR-siwis-medium.onnx
dl fr/fr_FR/siwis/medium/fr_FR-siwis-medium.onnx.json             fr_FR-siwis-medium.onnx.json
dl hi/hi_IN/pratham/medium/hi_IN-pratham-medium.onnx              hi_IN-pratham-medium.onnx
dl hi/hi_IN/pratham/medium/hi_IN-pratham-medium.onnx.json         hi_IN-pratham-medium.onnx.json
dl hu/hu_HU/anna/medium/hu_HU-anna-medium.onnx                    hu_HU-anna-medium.onnx
dl hu/hu_HU/anna/medium/hu_HU-anna-medium.onnx.json               hu_HU-anna-medium.onnx.json
dl is/is_IS/bui/medium/is_IS-bui-medium.onnx                      is_IS-bui-medium.onnx
dl is/is_IS/bui/medium/is_IS-bui-medium.onnx.json                 is_IS-bui-medium.onnx.json
dl it/it_IT/paola/medium/it_IT-paola-medium.onnx                  it_IT-paola-medium.onnx
dl it/it_IT/paola/medium/it_IT-paola-medium.onnx.json             it_IT-paola-medium.onnx.json
dl ka/ka_GE/natia/medium/ka_GE-natia-medium.onnx                  ka_GE-natia-medium.onnx
dl ka/ka_GE/natia/medium/ka_GE-natia-medium.onnx.json             ka_GE-natia-medium.onnx.json
dl kk/kk_KZ/raya/x_low/kk_KZ-raya-x_low.onnx                      kk_KZ-raya-x_low.onnx
dl kk/kk_KZ/raya/x_low/kk_KZ-raya-x_low.onnx.json                 kk_KZ-raya-x_low.onnx.json
dl lb/lb_LU/marylux/medium/lb_LU-marylux-medium.onnx              lb_LU-marylux-medium.onnx
dl lb/lb_LU/marylux/medium/lb_LU-marylux-medium.onnx.json         lb_LU-marylux-medium.onnx.json
dl lv/lv_LV/aivars/medium/lv_LV-aivars-medium.onnx                lv_LV-aivars-medium.onnx
dl lv/lv_LV/aivars/medium/lv_LV-aivars-medium.onnx.json           lv_LV-aivars-medium.onnx.json
dl ml/ml_IN/meera/medium/ml_IN-meera-medium.onnx                  ml_IN-meera-medium.onnx
dl ml/ml_IN/meera/medium/ml_IN-meera-medium.onnx.json             ml_IN-meera-medium.onnx.json
dl ne/ne_NP/chitwan/medium/ne_NP-chitwan-medium.onnx              ne_NP-chitwan-medium.onnx
dl ne/ne_NP/chitwan/medium/ne_NP-chitwan-medium.onnx.json         ne_NP-chitwan-medium.onnx.json
dl nl/nl_BE/nathalie/medium/nl_BE-nathalie-medium.onnx            nl_BE-nathalie-medium.onnx
dl nl/nl_BE/nathalie/medium/nl_BE-nathalie-medium.onnx.json       nl_BE-nathalie-medium.onnx.json
dl nl/nl_NL/ronnie/medium/nl_NL-ronnie-medium.onnx                nl_NL-ronnie-medium.onnx
dl nl/nl_NL/ronnie/medium/nl_NL-ronnie-medium.onnx.json           nl_NL-ronnie-medium.onnx.json
dl no/no_NO/talesyntese/medium/no_NO-talesyntese-medium.onnx      no_NO-talesyntese-medium.onnx
dl no/no_NO/talesyntese/medium/no_NO-talesyntese-medium.onnx.json no_NO-talesyntese-medium.onnx.json
dl pl/pl_PL/gosia/medium/pl_PL-gosia-medium.onnx                  pl_PL-gosia-medium.onnx
dl pl/pl_PL/gosia/medium/pl_PL-gosia-medium.onnx.json             pl_PL-gosia-medium.onnx.json
dl pt/pt_BR/faber/medium/pt_BR-faber-medium.onnx                  pt_BR-faber-medium.onnx
dl pt/pt_BR/faber/medium/pt_BR-faber-medium.onnx.json             pt_BR-faber-medium.onnx.json
dl ro/ro_RO/mihai/medium/ro_RO-mihai-medium.onnx                  ro_RO-mihai-medium.onnx
dl ro/ro_RO/mihai/medium/ro_RO-mihai-medium.onnx.json             ro_RO-mihai-medium.onnx.json
dl ru/ru_RU/dmitri/medium/ru_RU-dmitri-medium.onnx                ru_RU-dmitri-medium.onnx
dl ru/ru_RU/dmitri/medium/ru_RU-dmitri-medium.onnx.json           ru_RU-dmitri-medium.onnx.json
dl sk/sk_SK/lili/medium/sk_SK-lili-medium.onnx                    sk_SK-lili-medium.onnx
dl sk/sk_SK/lili/medium/sk_SK-lili-medium.onnx.json               sk_SK-lili-medium.onnx.json
dl sl/sl_SI/artur/medium/sl_SI-artur-medium.onnx                  sl_SI-artur-medium.onnx
dl sl/sl_SI/artur/medium/sl_SI-artur-medium.onnx.json             sl_SI-artur-medium.onnx.json
dl sr/sr_RS/serbski_institut/medium/sr_RS-serbski_institut-medium.onnx      sr_RS-serbski_institut-medium.onnx
dl sr/sr_RS/serbski_institut/medium/sr_RS-serbski_institut-medium.onnx.json sr_RS-serbski_institut-medium.onnx.json
dl sv/sv_SE/nst/medium/sv_SE-nst-medium.onnx                      sv_SE-nst-medium.onnx
dl sv/sv_SE/nst/medium/sv_SE-nst-medium.onnx.json                 sv_SE-nst-medium.onnx.json
dl sw/sw_CD/lanfrica/medium/sw_CD-lanfrica-medium.onnx            sw_CD-lanfrica-medium.onnx
dl sw/sw_CD/lanfrica/medium/sw_CD-lanfrica-medium.onnx.json       sw_CD-lanfrica-medium.onnx.json
dl tr/tr_TR/fettah/medium/tr_TR-fettah-medium.onnx                tr_TR-fettah-medium.onnx
dl tr/tr_TR/fettah/medium/tr_TR-fettah-medium.onnx.json           tr_TR-fettah-medium.onnx.json
dl uk/uk_UA/lada/x_low/uk_UA-lada-x_low.onnx                      uk_UA-lada-x_low.onnx
dl uk/uk_UA/lada/x_low/uk_UA-lada-x_low.onnx.json                 uk_UA-lada-x_low.onnx.json
dl vi/vi_VN/vais1000/medium/vi_VN-vais1000-medium.onnx            vi_VN-vais1000-medium.onnx
dl vi/vi_VN/vais1000/medium/vi_VN-vais1000-medium.onnx.json       vi_VN-vais1000-medium.onnx.json
dl zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx                zh_CN-huayan-medium.onnx
dl zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx.json           zh_CN-huayan-medium.onnx.json

echo "[piper-voices] done"
