namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Consulta a matriz normativa de derivação de modalidade, recortada para as modalidades que o
/// processo oferta — na mesma forma que <c>PUT …/regras-derivacao</c> recebe, para que o cliente
/// a envie sem transformação.
/// </summary>
/// <remarks>
/// A matriz é configuração normativa do ramo da Lei 12.711/2012 (red. Lei 14.723/2023), e vive no
/// domínio (<see cref="Domain.Services.RegrasDerivacaoModalidadeLei12711"/>) porque é ela que diz
/// quais opt-ins e quais elegibilidades compõem cada cota. Publicá-la por leitura é o que permite
/// ao cliente propô-la sem reescrever a lei do seu lado — uma segunda cópia envelheceria em
/// silêncio, e a primeira divergência só apareceria na classificação de um candidato real.
/// <para>
/// É <b>proposta</b>, não configuração: nada é gravado por esta leitura. Quem decide adotá-la, e
/// com que ajustes, é quem monta o edital — o processo do ramo institucional traz outra matriz.
/// </para>
/// </remarks>
public sealed record ObterRegrasDerivacaoNormativasQuery(Guid ProcessoSeletivoId)
    : IQuery<IReadOnlyList<ConfiguracaoDerivacaoDto>?>;
