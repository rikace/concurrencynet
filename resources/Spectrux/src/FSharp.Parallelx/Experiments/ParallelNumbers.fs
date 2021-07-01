namespace FSharp.Parallelx.State

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent

module ParallelNumbers = 


  let t = System.Diagnostics.Stopwatch.StartNew()

  let delta = sqrt Double.Epsilon  // epsilon_float

  [<Struct>]
  type vec =
    val x : float
    val y : float
    val z : float

    new(x, y, z) = {x=x; y=y; z=z}

    static member ( + ) (a : vec, b : vec) = new vec(a.x+b.x, a.y+b.y, a.z+b.z)
    static member ( - ) (a : vec, b : vec) = new vec(a.x-b.x, a.y-b.y, a.z-b.z)
    static member ( * ) (a, b : vec) = new vec(a*b.x, a*b.y, a*b.z)

  let vec x y z = new vec(x, y, z)

  let dot (a : vec) (b : vec) = a.x * b.x + a.y * b.y + a.z * b.z
  let length r = sqrt(dot r r)
  let unitise r = 1. / length r * r

  type scene = Scene of vec * float * scene_type
  and scene_type =
    | Sphere
    | Group of scene * scene * scene * scene * scene

  let infinity = System.Double.PositiveInfinity

  let inline ray_sphere (d : vec) (v : vec) r =
    let vx = v.x
    let vy = v.y
    let vz = v.z
    let disc = vx * vx + vy * vy + vz * vz - r * r
    if disc < 0. then infinity else
      let b = vx * d.x + vy * d.y + vz * d.z
      let b2 = b * b
      if b2 < disc then infinity else
        let disc = sqrt(b2 - disc)
        let t1 = b - disc in
        if t1 > 0. then t1 else b + disc

  let ray_sphere' (o : vec) (d : vec) (c : vec) r =
    let vx = c.x - o.x
    let vy = c.y - o.y
    let vz = c.z - o.z
    let vv = vx * vx + vy * vy + vz * vz
    let b = vx * d.x + vy * d.y + vz * d.z
    let disc = b * b - vv + r * r
    disc >= 0. && b + sqrt disc >= 0.

  type hit = {mutable l: float; mutable nx: float; mutable ny: float; mutable nz: float}

  let intersect_sphere (dir: vec) hit l' (center: vec) =
      let x = l' * dir.x - center.x
      let y = l' * dir.y - center.y
      let z = l' * dir.z - center.z
      let il = 1. / sqrt(x * x + y * y + z * z)
      hit.l <- l'
      hit.nx <- il * x
      hit.ny <- il * y
      hit.nz <- il * z

  let rec intersect dir hit = function
    | Scene(center, radius,typ) ->
        let l' = ray_sphere dir center radius
        if l' < hit.l then
            match typ with
            | Sphere ->
                intersect_sphere dir hit l' center
            | Group(a, b, c, d, e) ->
                intersect dir hit a
                intersect dir hit b
                intersect dir hit c
                intersect dir hit d
                intersect dir hit e

  let rec intersect' orig dir = function
    | Scene(center, radius, typ) ->
        ray_sphere' orig dir center radius &&
        match typ with
        | Sphere -> true
        | Group (a, b, c, d, e) ->
            intersect' orig dir a ||
            intersect' orig dir b ||
            intersect' orig dir c ||
            intersect' orig dir d ||
            intersect' orig dir e

  let neg_light = unitise(vec 1. 3. -2.)

  let rec ray_trace dir scene =
    let hit = {l=infinity; nx=0.; ny=0.; nz=0.}
    intersect dir hit scene;
    if hit.l = infinity then 0. else
      let n = vec hit.nx hit.ny hit.nz in
      let g = dot n neg_light in
      if g < 0. then 0. else
        if intersect' (hit.l * dir + delta * n) neg_light scene then 0. else g

  let fold5 f x a b c d e = f (f (f (f (f x a) b) c) d) e

  let rec create level c r =
    let obj = Scene(c, r,Sphere)
    if level = 1 then obj else
      let a = 3. * r / sqrt 12.
      let rec bound (c, r) = function
        | Scene(c', r',Sphere) -> c, max r (length (c - c') + r')
        | Scene(_, _, Group(v, w, x, y, z)) -> fold5 bound (c, r) v w x y z
      let aux x' z' = create (level - 1) (c + vec x' a z') (0.5 * r)
      let w, x, y, z = aux a (-a), aux (-a) (-a), aux a a, aux (-a) a
      let c, r = fold5 bound (c + vec 0. r 0., 0.) obj w x y z in
      Scene(c, r, Group(obj, w, x, y, z))

  let level, n = 11, 2048

  let scene = create level (vec 0. -1. 4.) 1.
  let ss = 4

  do
    use ch = System.IO.File.Create("/Users/riccardo/Downloads/image.pgm", n * n + 32)
    sprintf "P5\n%d %d\n255\n" n n
    |> String.iter (ch.WriteByte << byte)
    let image = Array.create (n*n) 0uy
    System.Threading.Tasks.Parallel.For(0, n, fun y ->
      for x = 0 to n - 1 do
        let mutable g = 0.
        for dx = 0 to ss - 1 do
          for dy = 0 to ss - 1 do
            let aux x d = float x - 0.5 * float n + float d / float ss
            let dir = unitise(vec (aux x dx) (aux y dy) (float n))
            g <- g + ray_trace dir scene
        image.[x+y*n] <- 0.5 + 255. * g / float (ss*ss) |> byte)
    |> ignore
    ch.Write(image, 0, n*n)
    printf "Took %gs" t.Elapsed.TotalSeconds

