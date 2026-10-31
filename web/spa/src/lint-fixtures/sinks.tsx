// This file is IGNORED by the normal lint run. src/lint-fixtures/sinks.test.ts lints it on purpose and
// checks that every banned sink below is reported. Do not copy any of this.
import { Link } from 'react-router'

export function Sinks({ html, url }: { html: string; url: string }) {
  const element = document.createElement('div')
  element.innerHTML = html // 1
  element.insertAdjacentHTML('beforeend', html) // 2
  element.setAttribute('href', url) // 3
  window.location.href = url // 4
  location.assign(url) // 5
  window.open(url) // 6
  document.write(html) // 7
  return (
    <div>
      <div dangerouslySetInnerHTML={{ __html: html }} /> {/* 8 */}
      <a href={url}>x</a> {/* 9 */}
      <iframe srcDoc={html} title="x" /> {/* 10 */}
      <form action={url} /> {/* 11 */}
      <Link to="https://evil.example">x</Link> {/* 12 */}
    </div>
  )
}
