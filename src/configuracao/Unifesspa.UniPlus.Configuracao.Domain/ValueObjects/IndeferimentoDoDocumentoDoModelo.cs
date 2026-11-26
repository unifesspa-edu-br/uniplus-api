namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O que acontece quando o candidato não apresenta o documento exigido pelo modelo: se o documento é
/// obrigatório, a consequência do indeferimento e as bases legais que a sustentam.
/// </summary>
public sealed record IndeferimentoDoDocumentoDoModelo(bool Obrigatorio, string? Consequencia, IReadOnlyList<BaseLegalDoModelo> BasesLegais, bool AbrePrazoParaReenvio = false);
