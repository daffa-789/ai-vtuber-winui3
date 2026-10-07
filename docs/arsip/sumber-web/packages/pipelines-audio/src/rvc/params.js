function paramAktif(k) {
  return {
    f0method: k.rvcF0,
    f0up_key: k.rvcTranspose,
    index_rate: k.rvcIndeksLaju,
    filter_radius: k.rvcPencucian,
    resample_sr: k.rvcResample,
    rms_mix_rate: k.rvcCampurRms,
    protect: k.rvcProteksi
  };
}
function sidikJariRvc(k, tandaCheckpoint, tandaIndex) {
  const p = paramAktif(k);
  const bagian = Object.keys(p).sort().map((kunci) => `${kunci}=${p[kunci]}`);
  bagian.push(tandaCheckpoint);
  bagian.push(p.index_rate ? tandaIndex : "tanpa-index");
  return bagian.join("|");
}
const LAJU_KELUARAN_RVC_DEFAULT = 4e4;
export {
  LAJU_KELUARAN_RVC_DEFAULT,
  paramAktif,
  sidikJariRvc
};
